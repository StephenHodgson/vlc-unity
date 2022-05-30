using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LibVLCSharp
{
    internal class OnLoad
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private const string UnityPlugin = "libVLCUnityPlugin";
#else
        private const string UnityPlugin = "VLCUnityPlugin";
#endif

        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_unity_set_color_space")]
        private static extern void SetColorSpace(UnityColorSpace colorSpace);

        [DllImport(UnityPlugin, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetRenderEventFunc();

        private enum UnityColorSpace
        {
            Gamma = 0,
            Linear = 1,
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoadRuntimeMethod()
        {
            SetColorSpace(PlayerColorSpace);
#if UNITY_ANDROID && !UNITY_EDITOR
            GL.IssuePluginEvent(GetRenderEventFunc(), 1);
#endif
        }

        private static UnityColorSpace PlayerColorSpace => QualitySettings.activeColorSpace == 0 ? UnityColorSpace.Gamma : UnityColorSpace.Linear;
    }
}