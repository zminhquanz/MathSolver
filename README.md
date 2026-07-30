# Math Solver

<p align="center">
  <img src="Resources/AppIcon/appicon.png"
       alt="Math Solver App Icon"
       width="150">
</p>

<p align="center">
  <a href="https://ko-fi.com/YOUR_KO_FI_USERNAME">
    <img src="https://img.shields.io/badge/Support_on-Ko--fi-FF5E5B?logo=ko-fi&logoColor=white"
         alt="Support Math Solver on Ko-fi">
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/License-MIT-yellow.svg"
         alt="MIT License">
  </a>
</p>

Math Solver is an offline-first mathematics learning and problem-solving application built with .NET MAUI. It provides step-by-step calculations, reusable formula references, responsive layouts, and high-precision numeric processing for students and anyone who wants to review essential mathematics.

The project is designed for cross-platform development, with the current implementation focused on Windows and Android.

## Features

### Solve Math

The **Solve Math** tab contains dedicated tools for:

- Basic arithmetic: addition, subtraction, multiplication, and division
- Integer and decimal calculations
- Long-division presentation
- Fraction addition, subtraction, multiplication, division, simplification, and common denominators
- Finding an unknown value in arithmetic equations
- Quadratic equations and parabola graphs
- Plane and solid geometry calculations

### Geometry Calculator

The geometry calculator reuses a shared `GeometryFormulaItem` catalog so that formulas, diagrams, symbols, and shape metadata remain consistent across the **Solve Math** and **Formulas** tabs.

Supported plane shapes include:

- Square
- Rectangle
- Triangle
- Right triangle
- Equilateral triangle
- Circle
- Trapezoid
- Isosceles trapezoid
- Right trapezoid
- Rhombus
- Parallelogram

Supported solid shapes include:

- Cube
- Rectangular prism
- Sphere
- Cylinder
- Cone

The calculator can determine values such as:

- Perimeter
- Area
- Base area
- Lateral surface area
- Total surface area
- Volume

Input and result handling:

- Integer input uses `Int128`
- Integer calculations and results use `BigInteger`
- Decimal input and results use `Decimal`
- Decimal input supports up to 10 digits after the decimal point
- Input values are validated against the limits of `Int128` and `Decimal`
- Decimal overflow is detected and reported
- Values containing more than 18 digits are displayed in scientific notation

### Quadratic Equations

The quadratic equation solver keeps coefficients in `decimal` form for accurate input and display.

Internal calculations use a custom Double-Double numeric structure for approximately 32 significant digits of precision. This improves the calculation of:

- Discriminant
- Square root of the discriminant
- Real roots
- Parabola vertex
- Parabola sampling points

`Math.FusedMultiplyAdd` is used where appropriate to reduce intermediate rounding error. Displayed decimal results are limited to 10 digits after the decimal point.

### Formula Reference

The **Formulas** tab includes:

- Rules for finding unknown components in addition, subtraction, multiplication, and division
- Detailed examples and verification steps
- Plane geometry formulas
- Solid geometry formulas
- Reusable diagrams and symbol descriptions

### Multiplication Tables

The **Multiplication Tables** tab provides:

- Multiplication tables from 1 to 10
- Division tables
- Responsive layouts for desktop and mobile screens

## User Interface

Math Solver includes:

- Responsive layouts for desktop, laptop, tablet, and phone screens
- Light and dark themes
- Custom accent colors
- Font customization
- Vietnamese and English localization
- Animated tab transitions
- Reusable vector and `GraphicsView` illustrations
- Adaptive card layouts based on the available screen width

## Offline Operation

The main calculation features work entirely offline. No internet connection or cloud-based AI service is required for standard arithmetic, fractions, equations, multiplication tables, formulas, or geometry calculations.

## Technology

- C#
- .NET MAUI
- XAML
- `GraphicsView` and custom `IDrawable` implementations
- `Int128`
- `BigInteger`
- `Decimal`
- Custom Double-Double arithmetic
- `Math.FusedMultiplyAdd`
- Responsive `Grid`, `FlexLayout`, and reusable `ContentView` components

## Project Structure

```text
MathSolver/
├── Controls/       Custom reusable controls
├── Graphics/       Shape, graph, and calculation drawables
├── Models/         Shared models and formula catalogs
├── Numerics/       High-precision numeric structures
├── Resources/      Images, icons, fonts, styles, and app resources
├── Services/       Localization, settings, and application services
├── Views/          Pages and reusable content views
├── App.xaml
├── AppShell.xaml
└── MauiProgram.cs
```

## Getting Started

### Requirements

Install the .NET SDK and .NET MAUI workload required by the project. For Windows development, use Visual Studio with the .NET MAUI development tools installed. Android development also requires the Android SDK and an emulator or physical device.

### Clone the Repository

```bash
git clone <your-repository-url>
cd MathSolver
```

### Restore Dependencies

```bash
dotnet restore
```

### Build

```bash
dotnet build
```

You can also open the solution in Visual Studio, select **Windows Machine** or an Android target, and run the application.

## Cleaning Build Artifacts

After replacing resources, XAML files, icons, or generated assets, remove the old build output before rebuilding:

```bash
dotnet clean
```

You may also delete the `bin` and `obj` folders, then rebuild the solution.

## Design Goals

Math Solver is developed with the following goals:

- Keep core mathematics features available offline
- Present calculations clearly instead of showing only final answers
- Preserve user input accurately
- Use appropriate numeric types for each calculation
- Share formulas and diagrams between learning and solving tools
- Maintain a clean and responsive interface across different screen sizes
- Avoid unnecessary subscriptions, advertisements, and online dependencies

## Educational Notice

Math Solver is intended to support learning, checking results, and understanding calculation steps. It should not replace independent practice or the guidance of a teacher.

## Support the Project

Math Solver is free to use, and all core features remain available without payment. Donations are completely optional and do not unlock additional features, subscriptions, or extra software rights.

If Math Solver is useful to you and you would like to support its continued development, you can leave a one-time tip on Ko-fi:

<p align="center">
  <a href="https://ko-fi.com/YOUR_KO_FI_USERNAME">
    <img src="https://img.shields.io/badge/Support_Math_Solver_on-Ko--fi-FF5E5B?logo=ko-fi&logoColor=white"
         alt="Support Math Solver on Ko-fi">
  </a>
</p>

Thank you for supporting the development, testing, documentation, and continued improvement of Math Solver.

> Replace `YOUR_KO_FI_USERNAME` in this file with your actual Ko-fi page name before publishing.

## License

The Math Solver source code is licensed under the [MIT License](LICENSE).

Copyright © 2026 Quan Vu.

The Math Solver name, application icon, logo, and original branding assets are not granted for reuse under the MIT License unless permission is provided separately.

## Status

The application is under active development. Additional formulas, geometry problems, calculation explanations, platform improvements, and interface refinements may be added over time.
