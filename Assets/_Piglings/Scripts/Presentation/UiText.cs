using System.Text;
using Piglings.Definitions;

namespace Piglings.Presentation
{
    /// <summary>
    /// Fills a UI Texts template (UiTextsDefinition): each {name} becomes its value — Fill("HOUR {hour}", ("hour", "3"))
    /// → "HOUR 3". A {name} with no value stays as typed, so a typo in the asset shows on screen instead of vanishing.
    /// Stateless.
    /// </summary>
    public static class UiText
    {
        public static string Fill(string template, params (string name, string value)[] values)
        {
            if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0) return template ?? "";
            var text = new StringBuilder(template);
            foreach (var (name, value) in values) text.Replace("{" + name + "}", value ?? "");
            return text.ToString();
        }

        /// <summary>"1 wolf" / "3 wolves", from the asset's two wolf texts.</summary>
        public static string Wolves(UiTextsDefinition texts, int count) =>
            Fill(count == 1 ? texts.oneWolf : texts.manyWolves, ("count", Numbers.Thousands(count)));
    }
}
