using LiveSplit.Drawing;
using System;
using System.Drawing;
using System.Globalization;
using System.Xml;

namespace LiveSplit.UI;

/// <summary>
/// Portable counterpart of LiveSplit.Core's SettingsHelper: the same XML parsing and writing
/// helpers, without the WinForms dialog helpers. Fonts and images are read and written in the
/// legacy BinaryFormatter format so files stay compatible with the Windows version.
/// </summary>
public class SettingsHelper
{
    public static string FormatFont(Font font)
    {
        return $"{font.FontFamily.Name} {font.Style}";
    }

    public static Color ParseColor(XmlElement colorElement, Color defaultColor = default)
    {
        return colorElement != null
            ? Color.FromArgb(int.Parse(colorElement.InnerText, NumberStyles.HexNumber))
            : defaultColor;
    }

    public static Font GetFontFromElement(XmlElement element)
    {
        if (element != null && !element.IsEmpty && !string.IsNullOrWhiteSpace(element.InnerText))
        {
            return LegacyBinarySerializer.ReadFont(Convert.FromBase64String(element.InnerText.Trim()));
        }

        return null;
    }

    public static int CreateSetting(XmlDocument document, XmlElement parent, string elementName, Font font)
    {
        if (document != null)
        {
            XmlElement element = document.CreateElement(elementName);

            if (font != null)
            {
                XmlCDataSection cdata = document.CreateCDataSection(Convert.ToBase64String(LegacyBinarySerializer.WriteFont(font)));
                element.InnerXml = cdata.OuterXml;
            }

            parent.AppendChild(element);
        }

        return font?.GetHashCode() ?? 0;
    }

    public static int CreateSetting(XmlDocument document, XmlElement parent, string elementName, Image image)
    {
        if (document != null)
        {
            XmlElement element = document.CreateElement(elementName);

            if (image != null)
            {
                XmlCDataSection cdata = document.CreateCDataSection(Convert.ToBase64String(LegacyBinarySerializer.WriteImage(image)));
                element.InnerXml = cdata.OuterXml;
            }

            parent.AppendChild(element);
        }

        return image != null ? image.GetHashCode() : 0;
    }

    public static Image GetImageFromElement(XmlElement element)
    {
        if (element != null && !element.IsEmpty && !string.IsNullOrWhiteSpace(element.InnerText))
        {
            return LegacyBinarySerializer.ReadImage(Convert.FromBase64String(element.InnerText.Trim()));
        }

        return null;
    }

    public static bool ParseBool(XmlElement boolElement, bool defaultBool = false)
    {
        return boolElement != null
            ? bool.Parse(boolElement.InnerText)
            : defaultBool;
    }

    public static bool TryParseBool(XmlElement boolElement, out bool result, bool defaultBool = false)
    {
        if (boolElement != null && bool.TryParse(boolElement.InnerText, out result))
        {
            return true;
        }

        result = defaultBool;
        return false;
    }

    public static int ParseInt(XmlElement intElement, int defaultInt = 0)
    {
        return intElement != null
            ? int.Parse(intElement.InnerText)
            : defaultInt;
    }

    public static bool TryParseInt(XmlElement intElement, out int result, int defaultInt = 0)
    {
        if (intElement != null && int.TryParse(intElement.InnerText, out result))
        {
            return true;
        }

        result = defaultInt;
        return false;
    }

    public static float ParseFloat(XmlElement floatElement, float defaultFloat = 0f)
    {
        return floatElement != null
            ? float.Parse(floatElement.InnerText.Replace(',', '.'), CultureInfo.InvariantCulture)
            : defaultFloat;
    }

    public static bool TryParseFloat(XmlElement floatElement, out float result, float defaultFloat = 0f)
    {
        if (floatElement != null && float.TryParse(floatElement.InnerText, out result))
        {
            return true;
        }

        result = defaultFloat;
        return false;
    }

    public static double ParseDouble(XmlElement doubleElement, double defaultDouble = 0.0)
    {
        return doubleElement != null
            ? double.Parse(doubleElement.InnerText, CultureInfo.InvariantCulture)
            : defaultDouble;
    }

    public static bool TryParseDouble(XmlElement doubleElement, out double result, double defaultDouble = 0.0)
    {
        if (doubleElement != null && double.TryParse(doubleElement.InnerText, out result))
        {
            return true;
        }

        result = defaultDouble;
        return false;
    }

    public static string ParseString(XmlElement stringElement, string defaultString = null)
    {
        defaultString ??= string.Empty;

        return stringElement != null
            ? stringElement.InnerText
            : defaultString;
    }

    public static TimeSpan ParseTimeSpan(XmlElement timeSpanElement, TimeSpan defaultTimeSpan = default)
    {
        return timeSpanElement != null
            ? TimeSpan.Parse(timeSpanElement.InnerText)
            : defaultTimeSpan;
    }

    public static bool TryParseTimeSpan(XmlElement timeSpanElement, out TimeSpan result,
        TimeSpan defaultTimeSpan = default)
    {
        if (timeSpanElement != null && TimeSpan.TryParse(timeSpanElement.InnerText, out result))
        {
            return true;
        }

        result = defaultTimeSpan;
        return false;
    }

    public static XmlElement ToElement<T>(XmlDocument document, string name, T value)
    {
        XmlElement element = document.CreateElement(name);
        element.InnerText = value?.ToString();
        return element;
    }

    public static int CreateSetting(XmlDocument document, XmlElement parent, string name, Color color)
    {
        if (document != null)
        {
            XmlElement element = document.CreateElement(name);
            element.InnerText = color.ToArgb().ToString("X8");
            parent.AppendChild(element);
        }

        return color.GetHashCode();
    }

    public static int CreateSetting<T>(XmlDocument document, XmlElement parent, string name, T value)
    {
        if (document != null)
        {
            XmlElement element = document.CreateElement(name);
            element.InnerText = value?.ToString();
            parent.AppendChild(element);
        }

        return value != null ? value.GetHashCode() : 0;
    }

    public static int CreateSetting(XmlDocument document, XmlElement parent, string name, float value)
    {
        if (document != null)
        {
            XmlElement element = document.CreateElement(name);
            element.InnerText = value.ToString(CultureInfo.InvariantCulture);
            parent.AppendChild(element);
        }

        return value.GetHashCode();
    }

    public static int CreateSetting(XmlDocument document, XmlElement parent, string name, double value)
    {
        if (document != null)
        {
            XmlElement element = document.CreateElement(name);
            element.InnerText = value.ToString(CultureInfo.InvariantCulture);
            parent.AppendChild(element);
        }

        return value.GetHashCode();
    }

    public static XmlAttribute ToAttribute<T>(XmlDocument document, string name, T value)
    {
        XmlAttribute element = document.CreateAttribute(name);
        element.Value = value?.ToString();
        return element;
    }

    public static T ParseEnum<T>(XmlElement element, T defaultEnum = default)
    {
        return element != null
            ? (T)Enum.Parse(typeof(T), element.InnerText)
            : defaultEnum;
    }

    public static bool TryParseEnum<T>(XmlElement element, out T result, T defaultEnum = default) where T : struct
    {
        if (element != null && Enum.TryParse(element.InnerText, out result))
        {
            return true;
        }

        result = defaultEnum;
        return false;
    }

    public static Version ParseVersion(XmlElement element)
    {
        return element != null
            ? Version.Parse(element.InnerText)
            : new Version(1, 0, 0, 0);
    }

    public static bool TryParseVersion(XmlElement element, out Version result)
    {
        if (element != null && Version.TryParse(element.InnerText, out result))
        {
            return true;
        }

        result = new Version(1, 0, 0, 0);
        return false;
    }

    public static Version ParseAttributeVersion(XmlElement element)
    {
        return element.HasAttribute("version")
            ? Version.Parse(element.GetAttribute("version"))
            : new Version(1, 0, 0, 0);
    }

    public static bool TryParseAttributeVersion(XmlElement element, out Version result)
    {
        if (element != null && element.HasAttribute("version")
            && Version.TryParse(element.GetAttribute("version"), out result))
        {
            return true;
        }

        result = new Version(1, 0, 0, 0);
        return false;
    }
}
