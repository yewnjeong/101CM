using UnityEngine;

namespace CM101
{
    /// <summary>
    /// 프로젝트에 미리 정의된 레이어 규칙을 따른다: 6 Solid, 7 Hazard, 8 Jelly, 9 Lead.
    /// 음식(트리거)만 10 Food를 추가로 쓴다.
    /// </summary>
    public static class Layers
    {
        public const int Solid = 6;
        public const int Hazard = 7;
        public const int Jelly = 8;
        public const int Lead = 9;
        public const int Food = 10;

        public const int EnvMask = (1 << 0) | (1 << Solid);          // 벽, 바닥, 선반
        public const int JellyMask = (1 << Jelly) | (1 << Lead);     // 선두 + 연결/분리 젤리
        public const int FoodMask = 1 << Food;                       // 일반 음식 + 최종 젤리 (트리거)
        public const int HazardMask = 1 << Hazard;                   // 떨어지는 상품, 청소기 몸체, 직원 발
    }

    public enum FoodType { Fruit, Bread, Cheese, Cookie, Cake }

    public enum LossType { Chunk, Scatter, Vacuum }

    public enum MsgKind { Info, Good, Warn, Gold }

    /// <summary>기획서 03장 음식 표를 코드로 옮긴 것. 증가량은 그대로 부여한다.</summary>
    public static class FoodData
    {
        static readonly Color[] FruitColors =
        {
            new Color(1f, 0.33f, 0.38f),  // 딸기/사과
            new Color(1f, 0.6f, 0.2f),    // 오렌지
            new Color(0.55f, 0.85f, 0.3f) // 청포도/키위
        };

        public static int Length(FoodType t)
        {
            switch (t)
            {
                case FoodType.Fruit: return 3;
                case FoodType.Bread: return 4;
                case FoodType.Cheese: return 5;
                case FoodType.Cookie: return 6;
                default: return 8; // Cake
            }
        }

        public static string DisplayName(FoodType t)
        {
            switch (t)
            {
                case FoodType.Fruit: return "과일 조각";
                case FoodType.Bread: return "빵 조각";
                case FoodType.Cheese: return "치즈 조각";
                case FoodType.Cookie: return "쿠키";
                default: return "케이크 조각";
            }
        }

        public static Color FoodColor(FoodType t, int variant)
        {
            switch (t)
            {
                case FoodType.Fruit: return FruitColors[Mathf.Abs(variant) % FruitColors.Length];
                case FoodType.Bread: return new Color(0.88f, 0.62f, 0.32f);
                case FoodType.Cheese: return new Color(1f, 0.84f, 0.25f);
                case FoodType.Cookie: return new Color(0.62f, 0.4f, 0.22f);
                default: return new Color(1f, 0.94f, 0.86f);
            }
        }

        /// <summary>연결 젤리 몸 색. 음식 색을 조금 밝게 해서 젤리 느낌을 낸다.</summary>
        public static Color JellyColor(FoodType t, int variant)
        {
            return Color.Lerp(FoodColor(t, variant), Color.white, 0.12f);
        }
    }
}
