using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UI_WrapLayoutGroup : LayoutGroup
{
    public float spacingX = 8f;
    public float spacingY = 8f;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
    }

    public override void CalculateLayoutInputVertical()
    {
        float width = rectTransform.rect.width;
        float x = padding.left;
        float y = padding.top;
        float rowHeight = 0f;

        foreach (RectTransform child in rectChildren)
        {
            float w = LayoutUtility.GetPreferredSize(child, 0);
            float h = LayoutUtility.GetPreferredSize(child, 1);

            if (x + w > width - padding.right)
            {
                x = padding.left;
                y += rowHeight + spacingY;
                rowHeight = 0f;
            }

            x += w + spacingX;
            rowHeight = Mathf.Max(rowHeight, h);
        }

        SetLayoutInputForAxis(y + rowHeight + padding.bottom, y + rowHeight + padding.bottom, -1, 1);
    }

    public override void SetLayoutHorizontal()
    {
     //   SetChildrenAlongAxis(0);
    }

    public override void SetLayoutVertical()
    {
        float width = rectTransform.rect.width;
        float x = padding.left;
        float y = padding.top;
        float rowHeight = 0f;

        foreach (RectTransform child in rectChildren)
        {
            float w = LayoutUtility.GetPreferredSize(child, 0);
            float h = LayoutUtility.GetPreferredSize(child, 1);

            if (x + w > width - padding.right)
            {
                x = padding.left;
                y += rowHeight + spacingY;
                rowHeight = 0f;
            }

            SetChildAlongAxis(child, 0, x, w);
            SetChildAlongAxis(child, 1, y, h);

            x += w + spacingX;
            rowHeight = Mathf.Max(rowHeight, h);
        }
    }
}
