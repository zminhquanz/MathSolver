# Bản đồ thư mục MathSolver

Project tổ chức theo **lớp trách nhiệm**, bên trong chia theo **chức năng**. Khi tìm hoặc thêm class, chọn lớp trước rồi chọn nhóm chức năng.

| Thư mục | Trách nhiệm |
|---|---|
| [Views](Views/README.md) | Trang và giao diện nhập liệu, hiển thị kết quả; XAML nằm cạnh code-behind. |
| [Services](Services/README.md) | Điều phối tính toán, sinh đề, AI, tùy chọn ứng dụng và dịch vụ dùng chung. |
| [Numerics](Numerics/README.md) | Kiểu số, kernel số học, SIMD, NTT/CRT và chuyển đổi số. |
| [Models](Models/README.md) | Dữ liệu đầu vào/kết quả, hợp đồng đề toán và gói ngôn ngữ. |
| [Graphics](Graphics/README.md) | Vẽ đồ thị, hình minh họa, biểu đồ benchmark và nền động. |
| [Controls](Controls/README.md) | Thành phần giao diện tái sử dụng: biểu thức, icon, tương tác và hình nền. |
| MarkupExtensions | Extension sử dụng trực tiếp trong XAML, chủ yếu phục vụ dịch thuật. |
| Platforms | Điểm khởi động, adapter và cấu hình riêng Android, Windows, iOS, MacCatalyst. |
| Resources | Ảnh, font, style, tài nguyên và nội dung bản dịch đóng gói cùng app. |
| Properties | Cấu hình chạy/debug của project. |

`App.xaml`, `AppShell.xaml`, `MauiProgram.cs` và `.csproj` ở thư mục gốc vì phụ trách khởi động, điều hướng và cấu hình toàn ứng dụng.

## Tìm nhanh theo bài toán

- **Lũy thừa:** `Views/Calculators/PowerRootView` → `Services/Calculations/PowerRootEngine` → `Numerics/Powers` và `Numerics/BigIntegers`; xuất số ở `Numerics/Serialization`.
- **Parabol/phương trình:** `Views/Calculators/QuadraticEquationView` → `Services/Calculations` → `Graphics/Equations`; kiểu số chính xác cao ở `Numerics/FloatingPoint`.
- **Chọn SIMD/số luồng:** `Views/Hardware` → `Services/Performance` → các kernel số học hoặc renderer sử dụng chính sách đó.
- **Sinh đề toán:** `Views/Quizzes` → `Services/Quizzes` hoặc `Services/AI` → `Models/Quizzes`.
- **Giao diện và ngôn ngữ:** `Views/Settings` → `Services/Appearance`, `Services/Localization`, `Services/Wallpaper`.

## Quy ước khi phát triển

- Namespace hiện có được giữ ổn định trong lần sắp xếp này; tên thư mục con thể hiện chức năng, không bắt buộc trùng namespace. Ví dụ `Services/Calculations/PowerRootEngine.cs` vẫn dùng `MathSolver.Services.Core`.
- Giữ XAML và `.xaml.cs` cùng thư mục; không đổi `x:Class` hoặc route chỉ vì đổi vị trí file.
- Các file partial của cùng một kiểu nằm cùng nhóm. `ParallelBigUnsigned` và hai file `_BinaryImport`, `_BinaryPower` thuộc `Numerics/BigIntegers`.
- Kernel số học không phụ thuộc Views; Views gọi Services để điều phối công việc.
- Khi di chuyển source, cập nhật cả đường dẫn `<Compile Include>` trong `../tests`, `<Compile Update>`/`<MauiXaml Update>` trong project và tài liệu liên quan.
- `bin`, `obj`, `../artifacts` là đầu ra sinh tự động, không dùng để chứa source. `../tests` chứa chương trình kiểm tra/benchmark; `../tools` chứa công cụ hỗ trợ.

Chi tiết thuật toán hiện có: [ARCHITECTURE.md](ARCHITECTURE.md).
