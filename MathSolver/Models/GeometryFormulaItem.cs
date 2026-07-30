using MathSolver.Graphics;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MathSolver.Models;

/// <summary>
/// Nhóm hình học dùng chung cho cả tab Công thức và tab Giải toán.
/// </summary>
public enum GeometryCategory
{
    Plane,
    Solid
}

/// <summary>
/// Mô tả đầy đủ một hình học. Đối tượng này không phụ thuộc vào FormulaPage,
/// vì vậy có thể tái sử dụng ở màn hình giải toán hình học sau này.
/// </summary>
public sealed class GeometryFormulaItem
{
    /// <summary>
    /// Khóa ổn định để tìm hình trong catalog, ví dụ: square, right_triangle.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    public GeometryCategory Category { get; init; }

    public GeometryShapeType ShapeType { get; init; }

    public string Name { get; init; } = string.Empty;

    public double CardHeight { get; init; }

    public IDrawable? Diagram { get; init; }

    public ObservableCollection<string> Formulas { get; init; } = [];

    public ObservableCollection<string> Symbols { get; init; } = [];
}

/// <summary>
/// Nguồn dữ liệu hình học dùng chung. FormulaPage chỉ lấy dữ liệu từ catalog,
/// còn tab Giải toán có thể gọi CreateAll/CreateByCategory/FindById để dùng lại
/// cùng tên hình, hình minh họa, công thức và chú thích.
/// </summary>
public static class GeometryFormulaCatalog
{
    public static IReadOnlyList<GeometryFormulaItem> CreateAll(
        Func<string, string>? translate = null)
    {
        string T(string text) =>
            translate?.Invoke(text) ?? text;

        return
        [
            Create(
                id: "square",
                category: GeometryCategory.Plane,
                shapeType: GeometryShapeType.Square,
                name: T("Hình vuông"),
                cardHeight: 500,
                formulas:
                [
                    T("Chu vi: P = a × 4"),
                    T("Diện tích: S = a × a")
                ],
                symbols:
                [
                    T("a: độ dài một cạnh")
                ]),

            Create(
                id: "rectangle",
                category: GeometryCategory.Plane,
                shapeType: GeometryShapeType.Rectangle,
                name: T("Hình chữ nhật"),
                cardHeight: 500,
                formulas:
                [
                    T("Chu vi: P = (a + b) × 2"),
                    T("Diện tích: S = a × b")
                ],
                symbols:
                [
                    T("a: chiều dài"),
                    T("b: chiều rộng")
                ]),

            Create(
                id: "triangle",
                category: GeometryCategory.Plane,
                shapeType: GeometryShapeType.Triangle,
                name: T("Hình tam giác"),
                cardHeight: 520,
                formulas:
                [
                    T("Chu vi: P = a + b + c"),
                    T("Diện tích: S = (a × h) ÷ 2")
                ],
                symbols:
                [
                    T("a: độ dài đáy"),
                    T("b, c: hai cạnh còn lại"),
                    T("h: chiều cao tương ứng với đáy a")
                ]),

            Create(
                id: "right_triangle",
                category: GeometryCategory.Plane,
                shapeType: GeometryShapeType.RightTriangle,
                name: T("Hình tam giác vuông"),
                cardHeight: 540,
                formulas:
                [
                    T("Chu vi: P = a + b + c"),
                    T("Diện tích: S = (a × b) ÷ 2"),
                    T("Quan hệ cạnh: c² = a² + b²")
                ],
                symbols:
                [
                    T("a, b: hai cạnh góc vuông"),
                    T("c: cạnh huyền"),
                    T("Góc giữa a và b bằng 90°")
                ]),

            Create(
                id: "equilateral_triangle",
                category: GeometryCategory.Plane,
                shapeType: GeometryShapeType.EquilateralTriangle,
                name: T("Hình tam giác đều"),
                cardHeight: 560,
                formulas:
                [
                    T("Chu vi: P = a × 3"),
                    T("Diện tích: S = (a × h) ÷ 2"),
                    T("Hoặc: S = (a² × √3) ÷ 4")
                ],
                symbols:
                [
                    T("a: độ dài mỗi cạnh"),
                    T("h: chiều cao, h = (a × √3) ÷ 2"),
                    T("Ba cạnh bằng nhau; ba góc đều bằng 60°")
                ]),

            Create(
                id: "circle",
                category: GeometryCategory.Plane,
                shapeType: GeometryShapeType.Circle,
                name: T("Hình tròn"),
                cardHeight: 530,
                formulas:
                [
                    T("Chu vi: C = 2 × π × r"),
                    T("Hoặc: C = π × d"),
                    T("Diện tích: S = π × r × r")
                ],
                symbols:
                [
                    T("r: bán kính"),
                    T("d: đường kính, d = 2 × r"),
                    T("π ≈ 3,14")
                ]),

            Create(
                id: "trapezoid",
                category: GeometryCategory.Plane,
                shapeType: GeometryShapeType.Trapezoid,
                name: T("Hình thang"),
                cardHeight: 530,
                formulas:
                [
                    T("Chu vi: P = a + b + c + d"),
                    T("Diện tích: S = ((a + b) × h) ÷ 2")
                ],
                symbols:
                [
                    T("a, b: hai đáy song song"),
                    T("c, d: hai cạnh bên"),
                    T("h: chiều cao")
                ]),

            Create(
                id: "isosceles_trapezoid",
                category: GeometryCategory.Plane,
                shapeType: GeometryShapeType.IsoscelesTrapezoid,
                name: T("Hình thang cân"),
                cardHeight: 550,
                formulas:
                [
                    T("Chu vi: P = a + b + 2c"),
                    T("Diện tích: S = ((a + b) × h) ÷ 2")
                ],
                symbols:
                [
                    T("a, b: hai đáy song song"),
                    T("c: hai cạnh bên bằng nhau"),
                    T("h: chiều cao")
                ]),

            Create(
                id: "right_trapezoid",
                category: GeometryCategory.Plane,
                shapeType: GeometryShapeType.RightTrapezoid,
                name: T("Hình thang vuông"),
                cardHeight: 550,
                formulas:
                [
                    T("Chu vi: P = a + b + c + h"),
                    T("Diện tích: S = ((a + b) × h) ÷ 2")
                ],
                symbols:
                [
                    T("a, b: hai đáy song song"),
                    T("h: cạnh bên vuông góc với hai đáy, đồng thời là chiều cao"),
                    T("c: cạnh bên còn lại")
                ]),

            Create(
                id: "rhombus",
                category: GeometryCategory.Plane,
                shapeType: GeometryShapeType.Rhombus,
                name: T("Hình thoi"),
                cardHeight: 550,
                formulas:
                [
                    T("Chu vi: P = a × 4"),
                    T("Diện tích: S = (d₁ × d₂) ÷ 2"),
                    T("Hoặc: S = a × h")
                ],
                symbols:
                [
                    T("a: độ dài một cạnh"),
                    T("d₁, d₂: hai đường chéo"),
                    T("h: chiều cao")
                ]),

            Create(
                id: "parallelogram",
                category: GeometryCategory.Plane,
                shapeType: GeometryShapeType.Parallelogram,
                name: T("Hình bình hành"),
                cardHeight: 530,
                formulas:
                [
                    T("Chu vi: P = (a + b) × 2"),
                    T("Diện tích: S = a × h")
                ],
                symbols:
                [
                    T("a: độ dài đáy"),
                    T("b: độ dài cạnh bên"),
                    T("h: chiều cao tương ứng với đáy a")
                ]),

            Create(
                id: "cube",
                category: GeometryCategory.Solid,
                shapeType: GeometryShapeType.Cube,
                name: T("Hình lập phương"),
                cardHeight: 550,
                formulas:
                [
                    T("Diện tích xung quanh: Sxq = 4 x a x a = 4a²"),
                    T("Diện tích toàn phần: Stp = 6 x a x a = 6a²"),
                    T("Thể tích: V = a x a x a = a³")
                ],
                symbols:
                [
                    T("a: độ dài một cạnh"),
                    T("Có 6 mặt là các hình vuông bằng nhau"),
                    T("Sxq gồm 4 mặt bên; Stp gồm cả 6 mặt")
                ]),

            Create(
                id: "rectangular_prism",
                category: GeometryCategory.Solid,
                shapeType: GeometryShapeType.RectangularPrism,
                name: T("Hình hộp chữ nhật"),
                cardHeight: 570,
                formulas:
                [
                    T("Diện tích xung quanh: Sxq = 2 × (a + b) × h"),
                    T("Diện tích toàn phần: Stp = 2 × (a × b + a × h + b × h)"),
                    T("Thể tích: V = a × b × h")
                ],
                symbols:
                [
                    T("a: chiều dài"),
                    T("b: chiều rộng"),
                    T("h: chiều cao")
                ]),

            Create(
                id: "sphere",
                category: GeometryCategory.Solid,
                shapeType: GeometryShapeType.Sphere,
                name: T("Hình cầu"),
                cardHeight: 520,
                formulas:
                [
                    T("Diện tích mặt cầu: S = 4 × π × r²"),
                    T("Thể tích: V = (4 × π × r³) ÷ 3")
                ],
                symbols:
                [
                    T("r: bán kính hình cầu"),
                    T("π ≈ 3,14")
                ]),

            Create(
                id: "cylinder",
                category: GeometryCategory.Solid,
                shapeType: GeometryShapeType.Cylinder,
                name: T("Hình trụ"),
                cardHeight: 590,
                formulas:
                [
                    T("Diện tích đáy: Sđ = π × r²"),
                    T("Diện tích xung quanh: Sxq = 2 × π × r × h"),
                    T("Diện tích toàn phần: Stp = 2 × π × r × (r + h)"),
                    T("Thể tích: V = π × r² × h")
                ],
                symbols:
                [
                    T("r: bán kính đáy"),
                    T("h: chiều cao"),
                    T("π ≈ 3,14")
                ]),

            Create(
                id: "cone",
                category: GeometryCategory.Solid,
                shapeType: GeometryShapeType.Cone,
                name: T("Hình nón"),
                cardHeight: 610,
                formulas:
                [
                    T("Diện tích đáy: Sđ = π × r²"),
                    T("Diện tích xung quanh: Sxq = π × r × l"),
                    T("Diện tích toàn phần: Stp = π × r × (r + l)"),
                    T("Thể tích: V = (π × r² × h) ÷ 3")
                ],
                symbols:
                [
                    T("r: bán kính đáy"),
                    T("h: chiều cao"),
                    T("l: đường sinh"),
                    T("π ≈ 3,14")
                ])
        ];
    }

    public static IReadOnlyList<GeometryFormulaItem> CreateByCategory(
        GeometryCategory category,
        Func<string, string>? translate = null)
    {
        return CreateAll(translate)
            .Where(item => item.Category == category)
            .ToArray();
    }

    public static GeometryFormulaItem? FindById(
        string id,
        Func<string, string>? translate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return CreateAll(translate)
            .FirstOrDefault(
                item => string.Equals(
                    item.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static GeometryFormulaItem Create(
        string id,
        GeometryCategory category,
        GeometryShapeType shapeType,
        string name,
        double cardHeight,
        IEnumerable<string> formulas,
        IEnumerable<string> symbols)
    {
        return new GeometryFormulaItem
        {
            Id = id,
            Category = category,
            ShapeType = shapeType,
            Name = name,
            CardHeight = cardHeight,
            Diagram = GeometryFormulaDrawableCache.Get(shapeType),
            Formulas = new ObservableCollection<string>(formulas),
            Symbols = new ObservableCollection<string>(symbols)
        };
    }

    private static class GeometryFormulaDrawableCache
    {
        private static readonly IReadOnlyDictionary<GeometryShapeType, IDrawable>
            Drawables =
                Enum.GetValues<GeometryShapeType>()
                    .ToDictionary(
                        shapeType => shapeType,
                        shapeType => (IDrawable)new GeometryShapeDrawable
                        {
                            ShapeType = shapeType
                        });

        public static IDrawable Get(
            GeometryShapeType shapeType)
        {
            return Drawables[shapeType];
        }
    }
}
