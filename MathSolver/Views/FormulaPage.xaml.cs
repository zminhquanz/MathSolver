using System.Collections.ObjectModel;
using MathSolver.Graphics;
using MathSolver.Models;

namespace MathSolver.Views;

public partial class FormulaPage : ContentPage
{
    private FormulaSubTab _selectedSubTab = FormulaSubTab.UnknownComponent;

    private int _currentSpan;

    public ObservableCollection<GeometryFormulaItem> GeometryItems { get; } = [];

    public ObservableCollection<UnknownComponentItem> UnknownComponentItems { get; } = [];

    public FormulaPage()
    {
        InitializeComponent();

        BindingContext = this;

        CreateGeometryItems();
        CreateUnknownComponentItems();

        SelectFormulaSubTab(
            FormulaSubTab.UnknownComponent,
            forceRefresh: true);
    }

    private void OnUnknownComponentTabClicked(
        object? sender,
        EventArgs e)
    {
        SelectFormulaSubTab(
            FormulaSubTab.UnknownComponent);
    }

    private void OnGeometryTabClicked(
        object? sender,
        EventArgs e)
    {
        SelectFormulaSubTab(
            FormulaSubTab.Geometry);
    }

    private void SelectFormulaSubTab(
        FormulaSubTab selectedTab,
        bool forceRefresh = false)
    {
        if (!forceRefresh &&
            _selectedSubTab == selectedTab)
        {
            return;
        }

        _selectedSubTab = selectedTab;

        bool showUnknown =
            selectedTab ==
            FormulaSubTab.UnknownComponent;

        UnknownComponentContent.IsVisible =
            showUnknown;

        GeometryContent.IsVisible =
            !showUnknown;

        UpdateSubTabStyles(showUnknown);
    }

    private void UpdateSubTabStyles(
        bool unknownSelected)
    {
        Color selectedBackground =
            Color.FromArgb("#6D28D9");

        Color selectedText =
            Colors.White;

        Color normalBackground =
            Colors.Transparent;

        Color normalText =
            Color.FromArgb("#475569");

        UnknownComponentTabButton.BackgroundColor =
            unknownSelected
                ? selectedBackground
                : normalBackground;

        UnknownComponentTabButton.TextColor =
            unknownSelected
                ? selectedText
                : normalText;

        GeometryTabButton.BackgroundColor =
            unknownSelected
                ? normalBackground
                : selectedBackground;

        GeometryTabButton.TextColor =
            unknownSelected
                ? normalText
                : selectedText;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0)
        {
            return;
        }

        int desiredSpan;
        double geometryCardHeight;

        if (width >= 1180)
        {
            desiredSpan = 3;
            geometryCardHeight = 570;
        }
        else if (width >= 720)
        {
            desiredSpan = 2;
            geometryCardHeight = 590;
        }
        else
        {
            desiredSpan = 1;
            geometryCardHeight = 620;
        }

        if (_currentSpan != desiredSpan)
        {
            _currentSpan = desiredSpan;

            UnknownComponentGridLayout.Span =
                desiredSpan;

            GeometryGridLayout.Span =
                desiredSpan;
        }

        foreach (GeometryFormulaItem item in GeometryItems)
        {
            item.CardHeight = geometryCardHeight;
        }
    }

    private void CreateUnknownComponentItems()
    {
        UnknownComponentItems.Clear();

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    "Tìm số hạng chưa biết",

                OperationSymbol =
                    "+",

                Structure =
                    "x + b = c",

                Rule =
                    "Muốn tìm một số hạng, lấy tổng trừ đi số hạng đã biết.",

                Example =
                    "x + 8 = 15",

                ExampleSolution =
                    "x = 15 − 8\nx = 7"
            });

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    "Tìm số bị trừ",

                OperationSymbol =
                    "−",

                Structure =
                    "x − b = c",

                Rule =
                    "Muốn tìm số bị trừ, lấy hiệu cộng với số trừ.",

                Example =
                    "x − 6 = 10",

                ExampleSolution =
                    "x = 10 + 6\nx = 16"
            });

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    "Tìm số trừ",

                OperationSymbol =
                    "−",

                Structure =
                    "a − x = c",

                Rule =
                    "Muốn tìm số trừ, lấy số bị trừ trừ đi hiệu.",

                Example =
                    "18 − x = 7",

                ExampleSolution =
                    "x = 18 − 7\nx = 11"
            });

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    "Tìm thừa số chưa biết",

                OperationSymbol =
                    "×",

                Structure =
                    "x × b = c",

                Rule =
                    "Muốn tìm một thừa số, lấy tích chia cho thừa số đã biết.",

                Example =
                    "x × 4 = 28",

                ExampleSolution =
                    "x = 28 ÷ 4\nx = 7"
            });

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    "Tìm số bị chia",

                OperationSymbol =
                    "÷",

                Structure =
                    "x ÷ b = c",

                Rule =
                    "Muốn tìm số bị chia, lấy thương nhân với số chia.",

                Example =
                    "x ÷ 5 = 9",

                ExampleSolution =
                    "x = 9 × 5\nx = 45"
            });

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    "Tìm số chia",

                OperationSymbol =
                    "÷",

                Structure =
                    "a ÷ x = c",

                Rule =
                    "Muốn tìm số chia, lấy số bị chia chia cho thương.",

                Example =
                    "42 ÷ x = 6",

                ExampleSolution =
                    "x = 42 ÷ 6\nx = 7"
            });
    }

    private void CreateGeometryItems()
    {
        GeometryItems.Clear();

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình vuông",
                CardHeight = 500,

                Diagram =
                    GeometryDrawableCache.Square,

                Formulas =
                {
                    "Chu vi: P = a × 4",
                    "Diện tích: S = a × a"
                },

                Symbols =
                {
                    "a: độ dài một cạnh"
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình chữ nhật",
                CardHeight = 500,

                Diagram =
                    GeometryDrawableCache.Rectangle,

                Formulas =
                {
                    "Chu vi: P = (a + b) × 2",
                    "Diện tích: S = a × b"
                },

                Symbols =
                {
                    "a: chiều dài",
                    "b: chiều rộng"
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình tam giác",
                CardHeight = 520,

                Diagram =
                    GeometryDrawableCache.Triangle,

                Formulas =
                {
                    "Chu vi: P = a + b + c",
                    "Diện tích: S = (a × h) ÷ 2"
                },

                Symbols =
                {
                    "a: độ dài đáy",
                    "b, c: hai cạnh còn lại",
                    "h: chiều cao tương ứng với đáy a"
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình tròn",
                CardHeight = 530,

                Diagram =
                    GeometryDrawableCache.Circle,

                Formulas =
                {
                    "Chu vi: C = 2 × π × r",
                    "Hoặc: C = π × d",
                    "Diện tích: S = π × r × r"
                },

                Symbols =
                {
                    "r: bán kính",
                    "d: đường kính, d = 2 × r",
                    "π ≈ 3,14"
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình thang",
                CardHeight = 530,

                Diagram =
                    GeometryDrawableCache.Trapezoid,

                Formulas =
                {
                    "Chu vi: P = a + b + c + d",
                    "Diện tích: S = ((a + b) × h) ÷ 2"
                },

                Symbols =
                {
                    "a, b: hai đáy song song",
                    "c, d: hai cạnh bên",
                    "h: chiều cao"
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình thoi",
                CardHeight = 550,

                Diagram =
                    GeometryDrawableCache.Rhombus,

                Formulas =
                {
                    "Chu vi: P = a × 4",
                    "Diện tích: S = (d₁ × d₂) ÷ 2",
                    "Hoặc: S = a × h"
                },

                Symbols =
                {
                    "a: độ dài một cạnh",
                    "d₁, d₂: hai đường chéo",
                    "h: chiều cao"
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình bình hành",
                CardHeight = 530,

                Diagram =
                    GeometryDrawableCache.Parallelogram,

                Formulas =
                {
                    "Chu vi: P = (a + b) × 2",
                    "Diện tích: S = a × h"
                },

                Symbols =
                {
                    "a: độ dài đáy",
                    "b: độ dài cạnh bên",
                    "h: chiều cao tương ứng với đáy a"
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình lập phương",
                CardHeight = 550,

                Diagram =
                    GeometryDrawableCache.Cube,

                Formulas =
                {
                    "Diện tích xung quanh: Sxq = 4 x a x a = 4a²",
                    "Diện tích toàn phần: Stp = 6 x a x a = 6a²",
                    "Thể tích: V = a x a x a = a³"
                },

                Symbols =
                {
                    "a: độ dài một cạnh",
                    "Có 6 mặt là các hình vuông bằng nhau",
                    "Sxq gồm 4 mặt bên; Stp gồm cả 6 mặt"
                }
            });
        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình hộp chữ nhật",
                CardHeight = 570,

                Diagram =
                    GeometryDrawableCache.RectangularPrism,

                Formulas =
                {
                    "Diện tích xung quanh: Sxq = 2 × (a + b) × h",
                    "Diện tích toàn phần: Stp = 2 × (a × b + a × h + b × h)",
                    "Thể tích: V = a × b × h"
                },

                Symbols =
                {
                    "a: chiều dài",
                    "b: chiều rộng",
                    "h: chiều cao"
                }
            });
        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình cầu",
                CardHeight = 520,

                Diagram =
                    GeometryDrawableCache.Sphere,

                Formulas =
                {
                    "Diện tích mặt cầu: S = 4 × π × r²",
                    "Thể tích: V = (4 × π × r³) ÷ 3"
                },

                Symbols =
                {
                    "r: bán kính hình cầu",
                    "π ≈ 3,14"
                }
            });
        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình trụ",
                CardHeight = 590,

                Diagram =
                    GeometryDrawableCache.Cylinder,

                Formulas =
                {
                    "Diện tích đáy: Sđ = π × r²",
                    "Diện tích xung quanh: Sxq = 2 × π × r × h",
                    "Diện tích toàn phần: Stp = 2 × π × r × (r + h)",
                    "Thể tích: V = π × r² × h"
                },

                Symbols =
                {
                    "r: bán kính đáy",
                    "h: chiều cao",
                    "π ≈ 3,14"
                }
            });
        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    "Hình nón",
                CardHeight = 610,

                Diagram =
                    GeometryDrawableCache.Cone,

                Formulas =
                {
                    "Diện tích đáy: Sđ = π × r²",
                    "Diện tích xung quanh: Sxq = π × r × l",
                    "Diện tích toàn phần: Stp = π × r × (r + l)",
                    "Thể tích: V = (π × r² × h) ÷ 3"
                },

                Symbols =
                {
                    "r: bán kính đáy",
                    "h: chiều cao",
                    "l: đường sinh",
                    "π ≈ 3,14"
                }
            });
    }

    private enum FormulaSubTab
    {
        UnknownComponent,
        Geometry
    }
}

internal static class GeometryDrawableCache
{
    public static GeometryShapeDrawable Square { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Square
        };

    public static GeometryShapeDrawable Rectangle { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Rectangle
        };

    public static GeometryShapeDrawable Triangle { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Triangle
        };

    public static GeometryShapeDrawable Circle { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Circle
        };

    public static GeometryShapeDrawable Trapezoid { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Trapezoid
        };

    public static GeometryShapeDrawable Rhombus { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Rhombus
        };

    public static GeometryShapeDrawable Parallelogram { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Parallelogram
        };

    public static GeometryShapeDrawable Cube { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Cube
        };
    public static GeometryShapeDrawable RectangularPrism { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.RectangularPrism
        };

    public static GeometryShapeDrawable Sphere { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Sphere
        };

    public static GeometryShapeDrawable Cylinder { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Cylinder
        };

    public static GeometryShapeDrawable Cone { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Cone
        };
}
