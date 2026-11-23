using System;
using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046: 열쇠 조각 표(Data/KeyPieces.csv)를 읽어 기억 파편의 플래그로 찾게 해 준다.
    /// 열: flag(파편 플래그), pieces(| 로 나눈 낱말 조각, 적힌 순서가 정답), leak1, leak2(새어 나오는 말).
    /// </summary>
    public static class KeyPieces
    {
        /// <summary>한 기억에서 쓰는 조각의 최대 수. 넘는 조각은 버린다.</summary>
        public const int MAX_PIECES = 6;
        /// <summary>열쇠 조각 맞추기를 여는 데 필요한 조각의 최소 수</summary>
        public const int MIN_PIECES = 2;

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();

        /// <summary>기억 하나의 열쇠 조각과 새어 나오는 말</summary>
        public class Entry
        {
            public string[] Pieces = Array.Empty<string>();
            public string Leak1 = "";
            public string Leak2 = "";
        }

        /// <summary>
        /// 열쇠 조각 표를 읽어 기억해 둔다. 부를 때마다 처음부터 다시 읽는다.
        /// asset(KeyPieces.csv)을 사용하며, 기억해 둔 표를 바꾸고 틀린 곳은 Console에 남긴다.
        /// </summary>
        public static void Load(TextAsset asset)
        {
            Entries.Clear();
            if (asset == null) return;
            var errors = new List<string>();
            foreach (Csv.Row row in Csv.Read(asset, errors))
            {
                string flag = row["flag"];
                if (flag.Length == 0) continue;
                string[] pieces = row.List("pieces");
                if (pieces.Length > MAX_PIECES) Array.Resize(ref pieces, MAX_PIECES);
                Entries[flag] = new Entry
                {
                    Pieces = pieces,
                    Leak1 = row["leak1"].Replace("\\n", "\n"),
                    Leak2 = row["leak2"].Replace("\\n", "\n"),
                };
            }
            foreach (string error in errors) Debug.LogError($"[KeyPieces.csv] {error}", asset);
        }

        /// <summary>
        /// 파편 플래그로 열쇠 조각 줄을 찾는다.
        /// flag를 사용하며, 찾은 줄 또는 (없으면) null을 반환한다.
        /// </summary>
        public static Entry Find(string flag)
        {
            return !string.IsNullOrEmpty(flag) && Entries.TryGetValue(flag, out Entry entry) ? entry : null;
        }
    }
}
