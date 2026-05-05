# AGENTS — Developer-facing agent instructions

Purpose
- Short, focused guidance to help AI coding agents be immediately productive in this repository.

Quick commands
- From repo root: `pnpm dev` or `pnpm desktop` (same as `pnpm --filter desktop start`).
- From `desktop/`: `pnpm start` (runs `electron-forge start`).
- Package: `pnpm desktop:package` / `pnpm desktop:make` from root, or `pnpm run package` / `pnpm run make` inside `desktop/`.
- Lint: `pnpm lint` from root, or `pnpm run lint` inside `desktop/`.

Where to look first
- Design system and visual tokens: [DESIGN.md](DESIGN.md)
- Frontend entry: `src/renderer/src/index.tsx` and `src/renderer/src/App.tsx`.
- UI components: `src/renderer/src/components/ui`.
- Hooks: `src/renderer/src/hooks`.

Principal rules for AI agents (concise)
- Keep React components small and single-responsibility. Prefer many small components over large files.
- One component per file. Prefer named exports for discoverability.
- Aim for reusable presentation + container separation: presentational components (UI) should be easily composable.
- If a component grows beyond a single clear responsibility, split it into child components and move shared logic into hooks or utilities.
- Prefer using shadcn components and the `shadcn` library already present in dependencies for consistency.
- Favor composition over prop-heavy components: small primitives + composition is preferred.
- Follow the visual style in [DESIGN.md](DESIGN.md) for colors, spacing, typography and shapes.
- Use existing conventions: TypeScript + React (TSX), Tailwind utility classes are present; keep classnames consistent with surrounding files.
 - Extract reusable logic into hooks; prefer centralizing shared application state and complex business logic in a `Zustand` store (avoid scattering shared state across many components).

Practical limits & hints
- Prefer files under ~200 lines. If a component approaches that size, split it.
- Keep each component's responsibilities focused: rendering, minor local state, and events. Put data fetching, complex state, and business logic into hooks or services.
- Place reusable UI primitives in `src/renderer/src/components/ui` and hooks in `src/renderer/src/hooks`.
 - Use `zustand` for app-wide or cross-cutting state; keep hooks focused on encapsulating UI behavior and selectors rather than owning large shared state graphs.

When modifying or adding components
- Link to the design tokens in [DESIGN.md](DESIGN.md) rather than copying values.
- Reuse `shadcn` components before creating new variants. If customization is required, wrap shadcn components in small adapters.
- Add concise JSDoc or a short README comment at the top of new components explaining purpose and expected props.
 - If state or logic is shared between multiple components, model it in a `Zustand` store and expose typed selectors/hooks (e.g., `useStore`, `useSelector`) so components remain thin and focused.
 - Prefer small hooks that read/write the `Zustand` store or expose composable behaviors; avoid embedding global business logic directly in component files.

Why this file helps agents
- Centralizes build/run commands and component conventions so agents avoid noisy or inconsistent edits.
- Directs agents to existing design tokens and UI primitives to maintain visual consistency.

Next suggested customizations
- Add a linter rule or CI check that flags files >200 LOC or components with >1 responsibility.
- Create a small `skill` that auto-suggests component splits and shadcn wrappers when large components are detected.

Questions / feedback
- If you want stricter limits (e.g., 150 lines) or specific naming conventions, say so and I'll update this file.
