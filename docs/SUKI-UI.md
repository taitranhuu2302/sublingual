# SukiUI 6.1.0 Components

> **Verified against**: NuGet package `SukiUI` `6.1.0`
> 
> **Primary namespace**: `SukiUI.Controls`
> 
> **XAML namespace**: `xmlns:suki="https://github.com/kikipoulet/SukiUI"`

## Scope

This document lists the public controls and related public UI types exported by `SukiUI.dll` version `6.1.0`.

It intentionally does **not** list standard Avalonia controls such as `Button`, `TextBox`, `ComboBox`, `DatePicker`, `ProgressBar`, `TabControl`, `TreeView`, or `DataGrid`, because those are provided by Avalonia packages, not by SukiUI itself.

## Core Controls

| Type | Base Type | Description |
|------|-----------|-------------|
| `BusyArea` | `UserControl` | Wraps content and displays a busy/loading overlay. |
| `CircleProgressBar` | `UserControl` | Circular progress indicator. |
| `CodeView` | `UserControl` | Code-display view supplied by SukiUI. |
| `ContentExpandControl` | `ContentControl` | Expandable content container. |
| `GlassCard` | `ContentControl` | Glass-style content card used heavily in SukiUI layouts. |
| `GroupBox` | `HeaderedContentControl` | SukiUI-styled group box. |
| `InfoBadge` | `HeaderedContentControl` | Small badge for status, count, or label content. |
| `InfoBar` | `ContentControl` | Inline notification or status bar. |
| `Loading` | `Control` | Animated loading indicator. |
| `PropertyGrid` | `UserControl` | Property editor surface for `INotifyPropertyChanged` objects. |
| `PropertyGridDialog` | `UserControl` | Dialog-oriented property grid view. |
| `PropertyGridWindow` | `SukiWindow` | Window wrapper around the property grid UI. |
| `SettingsLayout` | `UserControl` | Prebuilt settings page layout. |
| `SettingsLayoutItem` | `Control` | Item element used inside `SettingsLayout`. |
| `Stepper` | `TemplatedControl` | Step-by-step progress or wizard control. |
| `SukiBackground` | `Control` | Themed background renderer with shader/background style options. |
| `SukiMainHost` | `ContentControl` | Main host surface that can display background and overlay hosts. |
| `SukiTransitioningContentControl` | `TemplatedControl` | Content host with SukiUI transition behavior. |
| `SukiWindow` | `Window` | Main SukiUI window with theme-aware chrome and host support. |
| `VerticalStepper` | `TemplatedControl` | Vertical stepper control. |
| `VerticalStepperItem` | `TemplatedControl` | Item used by `VerticalStepper`. |
| `WaveProgress` | `UserControl` | Wave-style progress indicator. |

## Navigation

| Type | Base Type | Description |
|------|-----------|-------------|
| `SukiSideMenu` | `TreeView` | SukiUI side navigation menu. |
| `SukiSideMenuItem` | `TreeViewItem` | Item for `SukiSideMenu`. |
| `SukiStackPage` | `TemplatedControl` | Stack-style page container used with Suki navigation patterns. |

## Dialogs And Toasts

| Type | Base Type | Description |
|------|-----------|-------------|
| `SukiDialog` | `TemplatedControl` | Dialog visual/control type used by the dialog host system. |
| `SukiDialogHost` | `TemplatedControl` | Overlay host for modal dialogs. |
| `SukiMessageBoxHost` | `HeaderedContentControl` | Message-box style host with configurable header, icons, and action buttons. |
| `SukiToast` | `ContentControl` | Toast notification visual/control type. |
| `SukiToastHost` | `ItemsControl` | Overlay host for toast notifications. |

## Gauges And Meters

| Type | Base Type | Description |
|------|-----------|-------------|
| `RadialGauge` | `Panel` | Circular/radial gauge control. |
| `RadialGaugeSegment` | `object` | Segment definition for `RadialGauge`. |
| `HorizontalBarMeter` | `Panel` | Horizontal segmented meter control. |
| `VerticalBarMeter` | `Panel` | Vertical segmented meter control. |

Namespaces:

- `SukiUI.Controls.Gauges`
- `SukiUI.Controls.Gauges.HorizontalBarMeter`
- `SukiUI.Controls.Gauges.VerticalBarMeter`

## Glass Morphism

| Type | Base Type | Description |
|------|-----------|-------------|
| `BlurBackground` | `Control` | Blur/glass background effect control. |
| `SukiBlurBackground` | `UserControl` | UserControl wrapper for SukiUI blur background content. |

Namespace:

- `SukiUI.Controls.GlassMorphism`

## Touch Controls

| Type | Base Type | Description |
|------|-----------|-------------|
| `ClickToEditTextControl` | `UserControl` | Touch-friendly editable text surface. |
| `OnScreenKeyboard` | `UserControl` | On-screen keyboard control. |
| `TouchNavigationStack` | `UserControl` | Navigation stack UI for touch scenarios. |
| `MobileNumberPicker` | `UserControl` | Touch-focused numeric picker. |
| `MobileNumberPickerPopup` | `UserControl` | Popup used by `MobileNumberPicker`. |
| `MobilePicker` | `UserControl` | Generic touch-friendly picker. |
| `MobilePickerPopUp` | `UserControl` | Popup used by `MobilePicker`. |

Namespaces:

- `SukiUI.Controls.Touch`
- `SukiUI.Controls.Touch.MobileNumberPicker`
- `SukiUI.Controls.Touch.MobilePicker`

## Experimental Controls

These public types exist in `SukiUI 6.1.0`, but the namespace marks them as experimental and their API/stability should not be assumed.

| Type | Base Type | Description |
|------|-----------|-------------|
| `ChatUI` | `UserControl` | Experimental chat UI control. |
| `SukiDesktopEnvironment` | `UserControl` | Experimental desktop-environment style shell. |
| `InternalWindow` | `UserControl` | Internal window used by the experimental desktop environment. |
| `WindowManager` | `UserControl` | Window manager surface used by the experimental desktop environment. |

Namespaces:

- `SukiUI.Controls.Experimental`
- `SukiUI.Controls.Experimental.DesktopEnvironment`

## Supporting Public UI Types

These are public UI-related types exported by the assembly, but they are not primary standalone controls:

| Type | Kind | Notes |
|------|------|-------|
| `LoadingStyle` | enum | Visual style options for `Loading`. |
| `TitleBarVisibilityMode` | enum | Title bar visibility behavior for `SukiWindow`. |
| `VerticalStepItem` | model | Item model used by the vertical stepper controls. |
| `PropertyGridTemplateSelector` | resource dictionary | Template selector resource for property grid rendering. |

## PropertyGrid Support Types

SukiUI 6.1.0 also exports several public view-model/helper types used by `PropertyGrid`, including:

- `BoolViewModel`
- `ComplexTypeViewModel`
- `DateTimeOffsetViewModel`
- `DateTimeViewModel`
- `DecimalViewModel`
- `DoubleViewModel`
- `EnumViewModel`
- `FloatViewModel`
- `IntegerViewModel`
- `LongViewModel`
- `StringViewModel`
- `CategoryViewModel`
- `InstanceViewModel`
- `PropertyViewModelBase<T>`
- `IPropertyViewModel`
- `IPropertyViewModel<T>`

These are useful if you are extending the property-grid system, but they are not general-purpose visual controls.

## Practical Notes

- Use `SukiWindow` as the app window root when you want SukiUI's title bar, background system, and overlay host support.
- For overlays, the package exposes `SukiDialogHost` and `SukiToastHost`.
- In this repo, the XAML namespace already used is `xmlns:suki="https://github.com/kikipoulet/SukiUI"`.
- If you need standard controls like `Button`, `TextBox`, `ComboBox`, `DatePicker`, or `ProgressBar`, keep using Avalonia controls; SukiUI styles them, but does not define them as its own public controls.
