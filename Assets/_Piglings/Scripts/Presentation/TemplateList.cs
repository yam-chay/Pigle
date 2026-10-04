using System.Collections.Generic;
using UnityEngine;

namespace Piglings.Presentation
{
    /// <summary>
    /// Clones a laid-out template (a TemplateSlot) once per item, with an optional separator template between items (an
    /// arrow between depth rows, a peg marker between hours). The clones go where the template sits, in order, and the
    /// templates are hidden — so art placed before / after them (the moon, the sun) stays at the ends.
    /// Positions come from a Layout Group (Vertical / Horizontal) on the templates' parent: this only orders them.
    /// </summary>
    public static class TemplateList
    {
        public static List<TemplateSlot> Build(TemplateSlot item, GameObject separator, int count)
        {
            var built = new List<TemplateSlot>();
            if (item == null) return built;
            var parent = item.transform.parent;
            int index = item.transform.GetSiblingIndex();
            item.gameObject.SetActive(false);
            if (separator != null) separator.SetActive(false);

            for (int i = 0; i < count; i++)
            {
                if (i > 0 && separator != null)
                {
                    var between = Object.Instantiate(separator, parent);
                    between.name = $"{separator.name} {i}";
                    between.SetActive(true);
                    between.transform.SetSiblingIndex(index++);
                }
                var slot = Object.Instantiate(item, parent);
                slot.name = $"{item.name} {i + 1}";
                slot.gameObject.SetActive(true);
                slot.transform.SetSiblingIndex(index++);
                built.Add(slot);
            }
            return built;
        }
    }
}
