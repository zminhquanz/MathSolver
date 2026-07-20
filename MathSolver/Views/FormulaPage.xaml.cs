using MathSolver.Graphics;
using MathSolver.Models;
using System.Collections.ObjectModel;

namespace MathSolver.Views;

public partial class FormulaPage : ContentPage
{
    private FormulaSubTab _selectedSubTab = FormulaSubTab.UnknownComponent;

    public ObservableCollection<GeometryFormulaItem> GeometryItems { get; } = [];

    public ObservableCollection<UnknownComponentItem> UnknownComponentItems { get; } = [];

    public FormulaPage()
    {
        InitializeComponent();

        BindingContext =
            this;

        BindableLayout.SetItemsSource(
            UnknownComponentFlexLayout,
            UnknownComponentItems);

        BindableLayout.SetItemsSource(
            GeometryFlexLayout,
            GeometryItems);

        SelectFormulaSubTab(
            FormulaSubTab.UnknownComponent);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_selectedSubTab ==
            FormulaSubTab.UnknownComponent)
        {
            RefreshUnknownComponentLayout();
        }
        else
        {
            // Tạo lại các card để GraphicsView nhận màu theme hiện tại.
            RefreshGeometryLayout();
        }
    }

    private void RefreshUnknownComponentLayout()
    {
        BindableLayout.SetItemsSource(
            UnknownComponentFlexLayout,
            null);

        BindableLayout.SetItemsSource(
            UnknownComponentFlexLayout,
            UnknownComponentItems);

        Dispatcher.Dispatch(
            () =>
            {
                UpdateUnknownComponentCardWidths(
                    UnknownComponentFlexLayout.Width);

                Dispatcher.Dispatch(
                    () =>
                    {
                        UpdateUnknownComponentCardWidths(
                            UnknownComponentFlexLayout.Width);
                    });
            });
    }

    private void RefreshGeometryLayout()
    {
        BindableLayout.SetItemsSource(
            GeometryFlexLayout,
            null);

        BindableLayout.SetItemsSource(
            GeometryFlexLayout,
            GeometryItems);

        Dispatcher.Dispatch(
            () =>
            {
                UpdateGeometryCardWidths(
                    GeometryFlexLayout.Width);

                // BindableLayout có thể vừa mới tạo xong children.
                // Gọi lại một lần sau khi layout đầu tiên hoàn tất.
                Dispatcher.Dispatch(
                    () =>
                    {
                        UpdateGeometryCardWidths(
                            GeometryFlexLayout.Width);
                    });
            });
    }

    private void OnUnknownComponentTabClicked(object? sender, EventArgs e)
    {
        SelectFormulaSubTab(FormulaSubTab.UnknownComponent);
    }

    private void OnGeometryTabClicked(object? sender, EventArgs e)
    {
        SelectFormulaSubTab(FormulaSubTab.Geometry);
    }

    private void SelectFormulaSubTab(FormulaSubTab selectedTab)
    {
        _selectedSubTab = selectedTab;

        bool showUnknownComponent = selectedTab == FormulaSubTab.UnknownComponent;

        // Hiển thị đúng vùng nội dung theo tab đang chọn.
        UnknownComponentContent.IsVisible = showUnknownComponent;

        GeometryContent.IsVisible = !showUnknownComponent;

        UpdateSubTabButtonStyles();

        if (showUnknownComponent)
        {
            // Không tạo lại danh sách mỗi lần đổi tab.
            if (UnknownComponentItems.Count == 0)
            {
                CreateUnknownComponentItems();
            }

            RefreshUnknownComponentLayout();

            return;
        }

        if (GeometryItems.Count == 0)
        {
            CreateGeometryItems();
        }

        // CardHeight của từng hình chỉ là chiều cao tối thiểu.
        // Grid trong XAML dùng hàng Auto nên card có nhiều công thức
        // hoặc chú thích sẽ tự mở rộng theo nội dung.
        RefreshGeometryLayout();
    }

    private void UpdateSubTabButtonStyles()
    {
        ResetSubTabButton(UnknownComponentTabButton);
        ResetSubTabButton(GeometryTabButton);

        Button selectedButton =
            _selectedSubTab switch
            {
                FormulaSubTab.UnknownComponent => UnknownComponentTabButton,
                FormulaSubTab.Geometry => GeometryTabButton,
                _ => UnknownComponentTabButton
            };

        selectedButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");

        selectedButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");
    }

    private static void ResetSubTabButton(Button button)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            "SurfaceAltColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            "TextPrimaryColor");
    }

    protected override void OnSizeAllocated(
        double width,
        double height)
    {
        base.OnSizeAllocated(
            width,
            height);

        if (width <= 0)
        {
            return;
        }

        Dispatcher.Dispatch(
            () =>
            {
                if (_selectedSubTab ==
                    FormulaSubTab.UnknownComponent)
                {
                    UpdateUnknownComponentCardWidths(
                        UnknownComponentFlexLayout.Width);
                }
                else
                {
                    UpdateGeometryCardWidths(
                        GeometryFlexLayout.Width);
                }
            });
    }

    private void OnUnknownComponentFlexLayoutSizeChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is FlexLayout layout)
        {
            UpdateUnknownComponentCardWidths(
                layout.Width);
        }
    }

    private void UpdateUnknownComponentCardWidths(
        double availableWidth)
    {
        UpdateFlexCardWidths(
            UnknownComponentFlexLayout,
            availableWidth);
    }

    private void OnGeometryFlexLayoutSizeChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is FlexLayout layout)
        {
            UpdateGeometryCardWidths(
                layout.Width);
        }
    }

    private void UpdateGeometryCardWidths(
        double availableWidth)
    {
        UpdateFlexCardWidths(
            GeometryFlexLayout,
            availableWidth);
    }

    private static void UpdateFlexCardWidths(
        FlexLayout layout,
        double availableWidth)
    {
        if (availableWidth <= 0 ||
            layout.Children.Count == 0)
        {
            return;
        }

        // Mỗi Border có Margin="6" ở cả hai bên.
        const double horizontalMarginPerCard =
            12;

        // WidthRequest của Border cần chừa thêm phần padding và stroke.
        const double borderPaddingAndStroke =
            30;

        // Chừa khoảng trống cho scrollbar, sai số làm tròn và mép phải.
        const double rightSafetySpace =
            20;

        int columnCount =
            availableWidth switch
            {
                // Ba cột khi vùng nội dung đủ rộng để công thức
                // và chú thích không bị ép xuống quá nhiều dòng.
                >= 1040 => 3,

                // Tablet hoặc cửa sổ Windows cỡ vừa.
                >= 680 => 2,

                // Điện thoại hoặc cửa sổ hẹp.
                _ => 1
            };

        double totalMargins =
            columnCount *
            horizontalMarginPerCard;

        double outerWidthPerCard =
            Math.Floor(
                (availableWidth -
                 totalMargins -
                 rightSafetySpace) /
                columnCount);

        double requestedWidth =
            outerWidthPerCard -
            borderPaddingAndStroke;

        requestedWidth =
            Math.Clamp(
                requestedWidth,
                190,
                380);

        bool sizeChanged =
            false;

        foreach (IView child
                 in layout.Children)
        {
            if (child is not VisualElement element)
            {
                continue;
            }

            if (Math.Abs(
                    element.WidthRequest -
                    requestedWidth) < 1)
            {
                continue;
            }

            element.MinimumWidthRequest =
                0;

            element.WidthRequest =
                requestedWidth;

            sizeChanged =
                true;
        }

        if (!sizeChanged)
        {
            return;
        }

        layout.InvalidateMeasure();

        if (layout.Parent
            is VisualElement parent)
        {
            parent.InvalidateMeasure();
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