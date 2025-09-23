#if UNITY_IOS
namespace iOSNative
{
    public enum ThermalState
    {
        /// <summary>
        /// The thermal state is within normal limits.
        /// </summary>
        Nominal,

        /// <summary>
        /// The thermal state is slightly elevated.
        /// </summary>
        Fair,

        /// <summary>
        /// The thermal state is high.
        /// </summary>
        Serious,

        /// <summary>
        /// The thermal state is significantly impacting the performance of the system and the device needs to cool down.
        /// </summary>
        Critical,
    }
}
#endif