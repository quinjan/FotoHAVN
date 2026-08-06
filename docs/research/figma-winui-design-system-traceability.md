# Figma-to-WinUI design-system traceability

Research date: 2026-08-06

Resolves: [#50](https://github.com/quinjan/FotoHAVN/issues/50)

Scope: trace approved Figma variables, components, properties, variants, and reference states to FotoHAVN's WinUI 3 XAML/C# implementation without treating generated snippets as production code

## Decision-ready answer

Use a three-part traceability contract:

1. **Figma approval record:** publish the `FotoHAVN Design System` library, mark implementation-ready component sets and Product frames as ready for development, and retain their stable file/node URLs plus the approved version or library publication note.
2. **Repository mapping:** commit a small manifest that maps every approved Figma token, component, property, and state to an exact WinUI resource key, control/style symbol, dependency property, or visual-state name. Review this manifest with every design-system change.
3. **Verification:** validate the mapped XAML resources and component/state names in CI, then perform visual and accessibility checks against the approved Product frames at the map's required sizes and scaling cases.

Use Figma Code Connect **optionally, after reusable WinUI components exist**, to surface hand-authored production-shaped XAML snippets and source links in Dev Mode. It is suitable as a discoverability and handoff layer, but not as the canonical sync mechanism: Figma provides no native WinUI/XAML/C# integration, and Code Connect does not compile XAML, validate C# behavior, synchronize Figma variables into `ResourceDictionary` values, or prove visual/accessibility equivalence.

Do not build a custom WinUI Code Connect parser for v1. Figma's framework-agnostic template files can already emit arbitrary snippets and are the actively recommended path, whereas custom parsers are explicitly preview and likely to change. Figma also announced that framework-specific parsers will stop receiving support after August 17, 2026. [Figma Code Connect repository](https://github.com/figma/code-connect), [Figma template files](https://developers.figma.com/docs/code-connect/template-files/), [Figma custom parsers](https://developers.figma.com/docs/code-connect/custom-parsers/)

## Why the mapping must be explicit

Figma and WinUI have corresponding concepts, but they do not share a schema or runtime:

| Design contract | Figma representation | WinUI 3 representation | Traceability key |
| --- | --- | --- | --- |
| Primitive value | Variable | Typed XAML resource such as `Color`, `Double`, or `FontFamily` | Canonical token name plus Figma variable key and XAML `x:Key` |
| Semantic token | Aliased variable | `Color`/`SolidColorBrush`, metric, typography, or style resource | One semantic name used in both systems |
| Theme/context | Variable mode | `ResourceDictionary.ThemeDictionaries` and `{ThemeResource}` | Figma collection/mode plus WinUI dictionary key |
| Reusable component | Main component/component set | Built-in control style, `UserControl`, or templated custom control | Figma component node URL plus code path and symbol |
| Public configuration | Component property/variant axis | Dependency property, content property, style choice, or typed app input | Exact property-to-property mapping |
| Interactive visual state | Variant property such as `State=Pressed` | `VisualStateGroup`/`VisualState` | Exact state name and owning group |
| Product/runtime state | Approved reference frame | Explicit application/view-model state rendered by a page/control | Figma frame URL plus behavioral state identifier and test |

Figma variables support only color, number, string, and boolean types; they can alias same-type variables, carry values by mode, and be published to libraries. That makes them a strong token source, but not a direct serialization format for WinUI types such as `Thickness`, `CornerRadius`, `Brush`, or `FontWeight`. [Figma variables, collections, and modes](https://help.figma.com/hc/en-us/articles/14506821864087-Overview-of-variables-collections-and-modes)

WinUI reusable values belong in keyed resource dictionaries, which can be composed through merged dictionaries. `{ThemeResource}` resolves a keyed value and re-evaluates it when the active theme changes; Microsoft also requires consistent resource keys across applicable theme dictionaries. [Microsoft ResourceDictionary guidance](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-resource-dictionary), [Microsoft ThemeResource guidance](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/themeresource-markup-extension)

For FotoHAVN v1, keep **Light** as the sole authored brand mode in Figma. In WinUI, use semantic `{ThemeResource}` references where system theme or High Contrast can affect a value, and preserve Windows High Contrast behavior without designing an unrelated dark brand theme. This is an implementation recommendation based on Microsoft's theme-resource and accessibility guidance, not a requirement to add dark mode. [Microsoft XAML theme resources](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/xaml-theme-resources), [Microsoft accessibility overview](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-overview)

## Component and state mapping rules

Use independent Figma variant properties such as `Hierarchy`, `Size`, and `State`; do not encode several axes into compound variant names. Figma describes variants as predictable versions in a component set, while component properties expose deliberate customization such as text, boolean visibility, instance swap, slots, and variant values. [Figma variants](https://help.figma.com/hc/en-us/articles/39636737843735-Components-collection-Variants-and-component-set-fundamentals), [Figma component properties](https://help.figma.com/hc/en-us/articles/39636407507735-Components-collection-Component-property-fundamentals)

Map them as follows:

- visual interaction values such as `Default`, `PointerOver`, `Pressed`, `Disabled`, and `Focused` map to named WinUI `VisualState`s;
- structural choices such as hierarchy, size, icon presence, and destructive intent map to dependency properties or explicit styles, not transient visual states;
- text and replaceable content map to `Content`, typed dependency properties, or content/template properties;
- whole-product conditions such as busy, success, confirmation, Operator Assistance, countdown, Capture saved, and Photo Strip return map to application state identifiers and canonical Product frames; they are not all component variants;
- each mapping records accessibility requirements, including accessible name/status behavior and keyboard/focus expectations.

WinUI's `VisualStateManager` defines discrete control states and transitions in XAML. A reusable templated control supplies its default style and `ControlTemplate` from `Themes/Generic.xaml`; a custom property that participates in styling, binding, or animation should be a dependency property. [Microsoft VisualStateManager](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.visualstatemanager), [Microsoft WinUI templated controls](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/xaml-templated-controls-winui-3), [Microsoft custom dependency properties](https://learn.microsoft.com/en-us/windows/apps/develop/platform/xaml/custom-dependency-properties)

Prefer built-in WinUI controls and styles where their semantics fit. A new control deriving directly from `Control` has no default automation peer, so genuinely custom controls must expose the required UI Automation behavior. [Microsoft custom automation peers](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/custom-automation-peers)

## Minimum repository manifest

The later implementation spec should introduce one machine-readable mapping file. The exact format can be JSON, YAML, or validated source, but every row needs:

- stable contract ID and lifecycle status (`draft`, `approved`, or `deprecated`);
- Figma file key, node ID/URL, published component/variable key where available, and approved version/publication note;
- Figma collection, variable, component, property, variant, and state names;
- WinUI XAML resource key/type or code file and symbol;
- property and state translations, including values that intentionally have no one-to-one mapping;
- accessibility obligations and the visual/behavioral test that verifies the contract.

Use names as the human contract and stable Figma keys/node links as identity. Renames are then visible without losing the relationship. Figma's file API can retrieve selected nodes by file key and node ID and returns component metadata; the Variables API exposes variable IDs, stable keys, collections, modes, values, aliases, and bindings. [Figma file nodes API](https://developers.figma.com/docs/rest-api/file-endpoints/), [Figma Variables API](https://developers.figma.com/docs/rest-api/variables-endpoints/)

The Variables REST API is not a safe baseline dependency unless the team's Figma plan qualifies: Figma currently limits its read and write endpoints to Enterprise organizations, with full-seat and permission requirements for writes. If those requirements are met later, CI can compare published Figma variable values to the repository manifest; otherwise, publication review and the committed manifest remain the approval checkpoint. [Figma Variables API requirements](https://developers.figma.com/docs/rest-api/variables/)

## Code Connect verdict

### What works for WinUI

Figma's recommended template files are framework-agnostic TypeScript files. They bind a Figma component URL to a source path and component label, read selected-instance properties, and render an exact custom snippet. A FotoHAVN template can therefore emit XAML using `language: "xml"` and label it `WinUI 3`; a separate C# example would need `plaintext` or imperfect highlighting because `csharp` is not among the documented language values. [Figma template files](https://developers.figma.com/docs/code-connect/template-files/), [Figma Code Connect configuration](https://developers.figma.com/docs/code-connect/api/config-file/)

The CLI can create, parse, preview, publish, and unpublish mappings, so template syntax can be checked before publication. Published templates appear in Dev Mode for the mapped component and can link back to source. [Figma Code Connect CLI](https://developers.figma.com/docs/code-connect/cli-reference/), [Figma Code Connect quickstart](https://developers.figma.com/docs/code-connect/quickstart-guide/)

### Material limitations

- The only documented native parser values are React, HTML, Swift, and Compose; there is no WinUI, XAML, or C# parser. [Figma Code Connect configuration](https://developers.figma.com/docs/code-connect/api/config-file/)
- Templates control snippets, not implementation. They do not parse or type-check WinUI code, inspect `ResourceDictionary` values, generate visual states, or keep Figma and code synchronized.
- Figma variable code syntax supports Web, Android, and iOS snippets—not Windows—so it cannot directly name a WinUI resource key as a Windows platform binding. [Figma variable code syntax](https://help.figma.com/hc/en-us/articles/15145852043927-Create-and-manage-variables-and-collections)
- Code Connect requires an Organization or Enterprise plan and a full Design or Dev Mode seat. It must remain an optional enhancement until the FotoHAVN project's Figma plan and seats are confirmed. [Figma Code Connect repository](https://github.com/figma/code-connect)
- Code Connect UI can create broad cross-language links, but UI-created connections do not show code snippets in Inspect; CLI templates are required for production-shaped XAML examples. [Figma Code Connect UI setup](https://developers.figma.com/docs/code-connect/code-connect-ui-setup/)
- A custom C#/XAML parser is possible, but the API is preview and would add a parser product to maintain. It is not justified for FotoHAVN's first design-system pass. [Figma custom parsers](https://developers.figma.com/docs/code-connect/custom-parsers/)

## Recommended rollout gate

1. Build and approve the Figma token hierarchy, component property/variant contract, and Product reference-state matrix.
2. Publish/note an approved Figma version and record stable node links.
3. During `to-spec`, define the repository manifest schema, XAML dictionary boundaries, component ownership, and automated name/value/state checks.
4. During `implement`, create or refactor reusable WinUI resources and components, then populate the mapping manifest and verify every mapped state visually and through UI Automation/accessibility checks.
5. If the Figma plan supports Code Connect, add framework-agnostic templates only after the WinUI public component surface stabilizes. Parse/preview them in CI and publish them under a `WinUI 3` label.
6. Any urgent code-side correction must update the Figma artifact and mapping in the same change cycle; neither Code Connect nor generated Dev Mode snippets make that reconciliation automatic.

Figma's ready-for-development status, node links, and version comparison provide the approval/history layer, but they do not replace repository tests. Dev Mode exposes component properties and versions and can compare a top-level frame or component across version history. [Figma Dev Mode guide](https://help.figma.com/hc/en-us/articles/15023124644247-Guide-to-Dev-Mode), [Figma compare changes](https://help.figma.com/hc/en-us/articles/15023193382935-Compare-changes-in-Dev-Mode)

## Resolution

Adopt the explicit Figma-to-WinUI mapping manifest and verification workflow as the required traceability mechanism. Treat framework-agnostic Code Connect templates as a useful, plan-gated Dev Mode enhancement for XAML discoverability. Do not depend on native WinUI support, automatic token synchronization, or a custom Code Connect parser for v1.
