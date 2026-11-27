using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 씬 인스펙터에 적는 화면 문구 칸에 붙인다. 칸에는 씬 문구 파일(Data/Loc/Scene.csv)의 key를 저장하고,
    /// 게임은 Strings.Scene으로 그 key의 현재 언어 글을 쓴다. 인스펙터는 key 아래에 원문을 보여 주고 고칠 수 있게 한다.
    /// 메뉴 EyeMaze/번역/씬 문구 옮기기가 적힌 글을 key로 바꾼다.
    /// </summary>
    public class SceneTextAttribute : PropertyAttribute
    {
        /// <summary>원문을 고치는 칸의 줄 수</summary>
        public int Lines { get; }

        /// <summary>
        /// 씬 문구 칸으로 표시한다.
        /// lines(원문 칸의 줄 수)를 사용하며, Lines를 정한다.
        /// </summary>
        public SceneTextAttribute(int lines = 1)
        {
            Lines = Mathf.Max(1, lines);
        }
    }
}
