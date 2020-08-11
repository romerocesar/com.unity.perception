using System;
using System.IO;

namespace UnityEngine.Perception.Randomization.Scenarios
{
    public abstract class Scenario<T> : ScenarioBase where T : new()
    {
        public T constants = new T();

        public override string OnSerialize()
        {
            return JsonUtility.ToJson(constants, true);
        }

        public override void OnDeserialize(string json)
        {
            constants = JsonUtility.FromJson<T>(json);
        }

        public override void Serialize()
        {
            Directory.CreateDirectory(Application.dataPath + "/StreamingAssets/");
            using (var writer = new StreamWriter(serializedConstantsFilePath, false))
                writer.Write(OnSerialize());
        }

        public override void Deserialize()
        {
            if (!File.Exists(serializedConstantsFilePath))
                Debug.LogWarning($"JSON scenario constants file does not exist at path {serializedConstantsFilePath}");
            else
            {
                var jsonText = File.ReadAllText(serializedConstantsFilePath);
                OnDeserialize(jsonText);
            }
        }
    }
}
