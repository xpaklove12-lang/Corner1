using System;
using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 홍채 무늬 한 장. 파편 하나를 읽을 때마다 한 장씩 숨은 벽으로 겹쳐지고, 다 모으면 손님 고유의 홍채 무늬가 된다.
    /// 한 장은 같은 모양을 원 둘레에 symmetry번 돌려 찍은 회전 대칭 무늬다.
    /// 좌표는 미로와 같다: ring(0 = 가장 바깥), sector(층 기준, 반시계 방향).
    /// </summary>
    [Serializable]
    public class IrisPatternSheet
    {
        public enum Shape
        {
            [InspectorName("살 (방사 벽)")] Spokes,
            [InspectorName("띠 (원호 벽)")] Arc,
            [InspectorName("나선 (비스듬한 방사 벽)")] Spiral,
            [InspectorName("꽃잎 (양옆 방사 벽 + 아래 원호)")] Petal,
        }

        [Tooltip("Inspector에서 알아보기 위한 이름")]
        [SerializeField] private string _name = "무늬";
        [SerializeField] private Shape _shape = Shape.Spokes;
        [Tooltip("원 둘레에 몇 번 돌려 찍을지. 칸 수를 나누어떨어지게 하면 무늬가 고르다")]
        [SerializeField, Range(1, 16)] private int _symmetry = 4;
        [Tooltip("시작 고리 (0 = 가장 바깥). 띠와 꽃잎의 원호는 이 고리의 안쪽 경계에 그린다")]
        [SerializeField] private int _ring;
        [Tooltip("살과 나선이 안쪽으로 몇 고리에 걸치는지")]
        [SerializeField, Range(1, 6)] private int _span = 1;
        [Tooltip("첫 무늬의 칸 위치 (돌려 찍는 기준)")]
        [SerializeField] private int _sector;
        [Tooltip("띠와 꽃잎이 몇 칸 너비인지")]
        [SerializeField, Range(1, 8)] private int _width = 1;
        [Tooltip("나선이 안쪽으로 갈수록 도는 방향 (1 = 반시계, -1 = 시계)")]
        [SerializeField] private int _direction = 1;

        public string Name => _name;

        /// <summary>
        /// 이 장이 막는 벽 자리를 모은다. 미로에 없는 자리도 그대로 넣으므로 쓰는 쪽에서 걸러 낸다.
        /// sectors(미로 칸 수)와 이 장의 설정값을 사용하며, into에 (종류, 고리, 칸)을 더한다.
        /// </summary>
        public void CollectWalls(int sectors, List<(MazeWall.Kind kind, int ring, int sector)> into)
        {
            int count = Mathf.Max(1, _symmetry);
            int step = _direction >= 0 ? 1 : -1;
            for (int k = 0; k < count; k++)
            {
                int b = _sector + Mathf.RoundToInt(k * sectors / (float)count);
                switch (_shape)
                {
                    case Shape.Spokes:
                        for (int j = 0; j < _span; j++) into.Add((MazeWall.Kind.Radial, _ring + j, Wrap(b, sectors)));
                        break;
                    case Shape.Arc:
                        for (int j = 0; j < _width; j++) into.Add((MazeWall.Kind.Arc, _ring, Wrap(b + j, sectors)));
                        break;
                    case Shape.Spiral:
                        for (int j = 0; j < _span; j++) into.Add((MazeWall.Kind.Radial, _ring + j, Wrap(b + j * step, sectors)));
                        break;
                    case Shape.Petal:
                        // 방사 벽 (r, s)는 s칸과 s+1칸 사이라서, b칸부터 width칸을 감싸는 양옆은 b-1과 b+width-1이다.
                        into.Add((MazeWall.Kind.Radial, _ring, Wrap(b - 1, sectors)));
                        into.Add((MazeWall.Kind.Radial, _ring, Wrap(b + _width - 1, sectors)));
                        for (int j = 0; j < _width; j++) into.Add((MazeWall.Kind.Arc, _ring, Wrap(b + j, sectors)));
                        break;
                }
            }
        }

        /// <summary>
        /// 칸 번호를 0 ~ sectors-1 범위로 감는다.
        /// s와 sectors를 사용하며, 감긴 칸 번호를 반환한다.
        /// </summary>
        private static int Wrap(int s, int sectors) => ((s % sectors) + sectors) % sectors;
    }
}
