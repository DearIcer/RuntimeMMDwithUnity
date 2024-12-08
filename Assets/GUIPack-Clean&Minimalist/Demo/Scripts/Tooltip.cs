// Copyright (C) 2015-2023 ricimi - All rights reserved.
// This code can only be used under the standard Unity Asset Store End User License Agreement.
// A Copy of the Asset Store EULA is available at http://unity3d.com/company/legal/as_terms.

using UnityEngine;
using UnityEngine.EventSystems;

namespace Ricimi
{
    /// <summary>
    /// Basic tooltip class used throughout the demo.
    /// </summary>
    public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject tooltip;

        public float fadeTime = 0.1f;

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (tooltip != null)
            {
                StartCoroutine(Utils.FadeIn(tooltip.GetComponent<CanvasGroup>(), 1.0f, fadeTime));
            }
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (tooltip != null)
            {
                StartCoroutine(Utils.FadeOut(tooltip.GetComponent<CanvasGroup>(), 0.0f, fadeTime));
            }
        }
    }
}
