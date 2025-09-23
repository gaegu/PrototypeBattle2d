#if UNITY_IOS
using System.Runtime.InteropServices;

namespace iOSNative
{
    public static class ProcessInfoBridge
    {
        public static ThermalState ThermalState
        {
            get
            {
#if !UNITY_EDITOR && UNITY_IOS
                return (ThermalState) ThermalStateNative();
#endif
                return ThermalState.Nominal;
            }
        }

        #region P/Invoke

        [DllImport("__Internal", EntryPoint = "thermalState")]
        static extern int ThermalStateNative();

        #endregion P/Invoke
    }
}
#endif