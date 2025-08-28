using UnityEngine;

namespace BackendDev
{
    public enum BuildType
    {
        Dev,
        Staging,
        Production
    }
    public class BuildConfiguration : MonoBehaviour
    {
        
        public BuildType buildType = BuildType.Dev;
        
        public string GetRootCollection()
        {
            switch (buildType)
            {
                case BuildType.Dev:
                    return "dev";
                case BuildType.Staging:
                    return "staging";
                case BuildType.Production:
                    return "production";
                default:
                    return "dev";
            }
        }
    }

}
