using System;
using System.IO;
using UnityEngine;

namespace Golovomka
{
    public enum AccessResult { Opened, FirstLoopRequired, Locked, Wrong }

    [Serializable]
    public class AccessProgress
    {
        public int version = 1;
        public int source;
        public int cycle = 1;
        public bool firstLoopCompleted;
        public bool solved;
        public int hint;
        public int pendingHint;
        public int mistakes;

        public static AccessProgress NewGame()
        {
            int number;
            do { number = UnityEngine.Random.Range(1000, 10000); }
            while (Transform(number) == number);
            return new AccessProgress { source = number };
        }

        public static int Transform(int input)
        {
            int result = 0;
            for (int position = 0; position < 4; position++)
            {
                int divisor = position == 0 ? 1000 : position == 1 ? 100 : position == 2 ? 10 : 1;
                int digit = input / divisor % 10;
                int square = digit * digit;
                int selected = position % 2 == 0 ? square % 10 : square >= 10 ? square / 10 : square;
                result = result * 10 + selected;
            }
            return result;
        }

        public int ReversedRule()
        {
            int value = 0;
            string digits = source.ToString();
            for (int i = 0; i < 4; i++)
            {
                int square = (digits[i] - '0') * (digits[i] - '0');
                value = value * 10 + (i % 2 == 0 ? (square >= 10 ? square / 10 : square) : square % 10);
            }
            return value;
        }

        public AccessResult TryCode(int code, bool programLaunched, ref bool wrongLock)
        {
            if (solved) return AccessResult.Opened;
            if (!firstLoopCompleted) return AccessResult.FirstLoopRequired;
            if (programLaunched || wrongLock) return AccessResult.Locked;
            if (code == Transform(source)) { solved = true; return AccessResult.Opened; }
            wrongLock = true;
            mistakes++;
            pendingHint = code == ReversedRule() ? 2 : 1;
            if (mistakes >= 2) pendingHint = 2;
            return AccessResult.Wrong;
        }

        public void NextCycle(bool programLaunched)
        {
            if (programLaunched) firstLoopCompleted = true;
            cycle++;
            hint = Math.Max(hint, pendingHint);
            pendingHint = 0;
        }
    }

    public static class ProgressStore
    {
        public static string PathName => Path.Combine(Application.persistentDataPath, "access-mvp.json");
        public static string LastError { get; private set; }

        public static AccessProgress Load(string file = null)
        {
            LastError = null;
            string path = file ?? PathName;
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var data = JsonUtility.FromJson<AccessProgress>(File.ReadAllText(candidate));
                    if (data != null && data.version == 1 && data.source >= 1000 && data.source <= 9999 && data.cycle > 0)
                        return data;
                }
                catch (Exception e) { LastError = e.Message; }
            }
            return null;
        }

        public static bool Save(AccessProgress data, string file = null)
        {
            try
            {
                string path = file ?? PathName;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
                else File.Move(path + ".tmp", path);
                LastError = null;
                return true;
            }
            catch (Exception e) { LastError = e.Message; Debug.LogWarning("Save failed: " + e.Message); return false; }
        }
    }
}
