// Copyright (C) 2015-2023 ricimi - All rights reserved.
// This code can only be used under the standard Unity Asset Store End User License Agreement.
// A Copy of the Asset Store EULA is available at http://unity3d.com/company/legal/as_terms.

using UnityEngine;

namespace Ricimi
{
    /// <summary>
    /// Utility component to open a URL.
    /// </summary>
    public class URLOpener : MonoBehaviour
    {
        public string URL;

        public void OpenURL()
        {
            Application.OpenURL(URL);
        }
    }
}