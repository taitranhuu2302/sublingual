---
name: LingoStream Core
colors:
  surface: '#0b1326'
  surface-dim: '#0b1326'
  surface-bright: '#31394d'
  surface-container-lowest: '#060e20'
  surface-container-low: '#131b2e'
  surface-container: '#171f33'
  surface-container-high: '#222a3d'
  surface-container-highest: '#2d3449'
  on-surface: '#dae2fd'
  on-surface-variant: '#c2c6d6'
  inverse-surface: '#dae2fd'
  inverse-on-surface: '#283044'
  outline: '#8c909f'
  outline-variant: '#424754'
  surface-tint: '#adc6ff'
  primary: '#adc6ff'
  on-primary: '#002e6a'
  primary-container: '#4d8eff'
  on-primary-container: '#00285d'
  inverse-primary: '#005ac2'
  secondary: '#d0bcff'
  on-secondary: '#3c0091'
  secondary-container: '#571bc1'
  on-secondary-container: '#c4abff'
  tertiary: '#4cd7f6'
  on-tertiary: '#003640'
  tertiary-container: '#009eb9'
  on-tertiary-container: '#002f38'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#d8e2ff'
  primary-fixed-dim: '#adc6ff'
  on-primary-fixed: '#001a42'
  on-primary-fixed-variant: '#004395'
  secondary-fixed: '#e9ddff'
  secondary-fixed-dim: '#d0bcff'
  on-secondary-fixed: '#23005c'
  on-secondary-fixed-variant: '#5516be'
  tertiary-fixed: '#acedff'
  tertiary-fixed-dim: '#4cd7f6'
  on-tertiary-fixed: '#001f26'
  on-tertiary-fixed-variant: '#004e5c'
  background: '#0b1326'
  on-background: '#dae2fd'
  surface-variant: '#2d3449'
typography:
  headline-xl:
    fontFamily: Inter
    fontSize: 48px
    fontWeight: '700'
    lineHeight: '1.1'
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '600'
    lineHeight: '1.2'
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: '1.3'
  body-lg:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '400'
    lineHeight: '1.6'
  body-md:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: '1.5'
  body-sm:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.5'
  label-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: '1'
    letterSpacing: 0.05em
  subtitle-high-contrast:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '500'
    lineHeight: '1.4'
    letterSpacing: 0.01em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  base: 8px
  xs: 4px
  sm: 12px
  md: 24px
  lg: 40px
  xl: 64px
  gutter: 24px
  margin: 32px
---

## Brand & Style

This design system is engineered for high-performance desktop productivity, focusing on cognitive clarity and technical precision. The brand personality is sophisticated, reliable, and "developer-adjacent," appealing to power users who value speed and deep work. 

The aesthetic leverages a **Modern Technical** style, blending the structured reliability of corporate SaaS with the immersive qualities of **Glassmorphism**. High-contrast typography ensures that information—particularly subtitles and data streams—remains legible against deep, layered backgrounds. The overall emotional response should be one of focused immersion and professional-grade capability.

## Colors

The color palette is anchored in deep, desaturated tones to minimize eye strain during long-form productivity sessions. 

- **Primary & Secondary:** Electric Blue (#3B82F6) serves as the primary action color, while Violet (#8B5CF6) is reserved for secondary indicators, progress states, and brand highlights.
- **Backgrounds:** A hierarchy of deep charcoals and slate grays (ranging from #020617 to #1E293B) creates a sense of spatial depth.
- **Accents:** High-vibrancy blues and purples are used sparingly to signal activity, selection, or live streams.
- **Text:** Primary text is near-white (#F8FAFC), while subtitles and metadata utilize a high-contrast slate-gray (#94A3B8) to ensure WCAG compliance without competing for visual attention.

## Typography

The design system utilizes **Inter** for all typographic layers to maintain a clean, utilitarian aesthetic. 

- **Hierarchy:** Strong contrast between headline weights (700/600) and body weights (400) guides the user's eye through complex data.
- **Subtitles:** A specific "subtitle-high-contrast" style is defined for accessibility during streaming playback, featuring increased font size and tracking to ensure readability against dynamic backgrounds.
- **Labels:** Small-caps or increased tracking should be applied to labels to distinguish metadata from actionable content.

## Layout & Spacing

This design system follows a **12-column fluid grid** model optimized for desktop environments. 

- **Grid:** A 24px gutter provides ample breathing room between functional modules, while a 32px outer margin anchors the layout.
- **Rhythm:** All vertical and horizontal spacing is based on an 8px square grid to maintain mathematical consistency.
- **Density:** The layout supports both a "Standard" and "Compact" view, with the latter reducing the base spacing unit to 4px for data-heavy dashboard views.

## Elevation & Depth

Visual hierarchy is established through **Tonal Layering** and **Glassmorphism**. 

- **Base Layer:** The lowest surface uses the darkest charcoal (#020617).
- **Surface Layer:** Cards and panels use a slightly lighter slate (#0F172A).
- **Glass Overlays:** Modals, dropdowns, and floating toolbars utilize a backdrop-blur (minimum 12px) with a semi-transparent slate fill (alpha 0.6) and a 1px inner border to simulate a glass-like refractive edge.
- **Shadows:** Shadows are rarely used for depth; instead, 1px borders in slightly lighter shades of gray define the edges of components.

## Shapes

The shape language is controlled and geometric. A standard **8px (0.5rem)** radius is applied to almost all components, including buttons, input fields, and card containers. 

- **Large Components:** Sections or large containers may scale up to a 1rem radius to soften the layout.
- **Small Components:** Checkboxes and tags maintain a tighter 4px radius to feel precise and technical.
- **Consistency:** Rounded corners should be consistent across all interactive elements to reinforce the modern, approachable nature of the technical interface.

## Components

- **Buttons:** Primary buttons feature a solid Electric Blue fill with white text. Secondary buttons use a ghost style with a 1px slate border.
- **Inputs:** Fields should have a dark, inset background (#020617) with an 8px radius. On focus, the border transitions to Electric Blue with a subtle outer glow.
- **Cards:** Use the "Surface Layer" slate color. For featured content, apply the Glassmorphism effect with a subtle 1px border (#334155).
- **Icons:** Use thin-line style (1.5px stroke weight) with open paths. Icons should be monochrome, adopting the Primary Blue color only when active.
- **Chips/Tags:** Small, pill-shaped indicators with a 10% opacity Violet background and solid Violet text to categorize content streams.
- **Lists:** Rows should be separated by high-contrast, low-opacity dividers (white @ 5%) with a hover state that lightens the background by 2%.
