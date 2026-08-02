# Math Solver

<p align="center">
  <img src="MathSolver/Resources/AppIcon/appicon.png"
       alt="Math Solver App Icon"
       width="150">
</p>

<p align="center">
  <a href="https://ko-fi.com/quanvu96">
    <img src="https://img.shields.io/badge/Support_on-Ko--fi-FF5E5B?logo=ko-fi&logoColor=white"
         alt="Support Math Solver on Ko-fi">
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/License-GPL_v3-blue.svg"
         alt="GNU GPL v3 License">
  </a>
</p>

Math Solver is an offline-first mathematics learning and problem-solving application built with .NET MAUI. It provides step-by-step calculations, reusable formula references, responsive layouts, and high-precision numeric processing for students and anyone who wants to review essential mathematics.

## Current Release

The current stable release is **Math Solver v0.1.1**.

The current public builds are available for Windows x64.

Android, macOS, and iOS releases are planned but are not available yet.

Download the latest version from the
[GitHub Releases](../../releases/latest) page.

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

### Quadratic Equations

Internal calculations use a custom Double-Double numeric structure for approximately 32 significant digits of precision. This improves the calculation of:

- Discriminant
- Square root of the discriminant
- Real roots
- Parabola vertex
- Parabola sampling points

### Formula Reference

The **Formulas** tab includes:

- Rules for finding unknown components in addition, subtraction, multiplication, and division
- Detailed examples and verification steps
- Plane geometry formulas
- Solid geometry formulas
- Reusable diagrams and symbol descriptions

### Multiplication Tables

The **Multiplication Tables** tab provides:

- Multiplication tables from 1 to 20
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

## Project Structure

```text
MathSolver/
├── Controls/             Custom reusable controls
├── Graphics/             Shape, graph, and calculation drawables
├── MarkupExtensions/     Markup Extensions
├── Models/               Shared models and formula catalogs
├── Numerics/             High-precision numeric structures
├── Platforms/            Platforms support
├── Resources/            Images, icons, fonts, styles, and app resources
├── Services/             Localization, settings, and application services
├── Views/                Pages and reusable content views
├── App.xaml
├── App.xaml.cs
├── AppShell.xaml
├── AppShell.xaml.cs
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
  <a href="https://ko-fi.com/quanvu96">
    <img src="https://img.shields.io/badge/Support_Math_Solver_on-Ko--fi-FF5E5B?logo=ko-fi&logoColor=white"
         alt="Support Math Solver on Ko-fi">
  </a>
</p>

Thank you for supporting the development, testing, documentation, and continued improvement of Math Solver.

## License

The Math Solver source code is free software licensed under the
[GNU General Public License version 3](LICENSE)

You may use, study, modify, and redistribute the GPL-covered source code.
If you distribute a modified version or a compiled binary based on Math Solver,
you must also make the complete corresponding source code available under the
same GNU GPL v3 license.

Copyright © 2026 Quan Vu.

The **Math Solver** name, application icon, logo, screenshots, and original
branding assets are not licensed for independent reuse or for branding modified
distributions. Forks and modified distributions must use their own name and
branding unless separate written permission is granted by the project author.
This branding restriction does not limit the rights granted for the
GPL-covered source code.

## Status

The application is under active development. Additional formulas, geometry problems, calculation explanations, platform improvements, and interface refinements may be added over time.
