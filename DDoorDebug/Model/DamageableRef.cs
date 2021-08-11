using System;
namespace DDoorDebug.Model
{
    public class DamageableRef
    {
        public DamageableCharacter instance;
        public float trackedHealth;
        public string stringHealth = String.Empty;

        public DamageableRef(DamageableCharacter inst, float tracked, string currentStr)
        {
            instance = inst;
            trackedHealth = tracked;
            stringHealth = currentStr;
        }
    }
}
