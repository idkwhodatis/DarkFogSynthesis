using System.Collections.Generic;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>Supported vanilla research-matrix ordering. This validates configuration, not animation behavior.</summary>
    public static class NativeMatrixContract
    {
        public static bool IsSupported(IReadOnlyList<int>? matrixIds)
        {
            if (matrixIds == null || matrixIds.Count != 6) return false;
            for (int i = 0; i < matrixIds.Count; ++i) if (matrixIds[i] != 6001 + i) return false;
            return true;
        }
    }
}
