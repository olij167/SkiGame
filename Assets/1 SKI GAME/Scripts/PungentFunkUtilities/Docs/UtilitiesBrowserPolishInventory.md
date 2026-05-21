# Utilities Browser Polish Inventory

## Before

- Entry points: Utilities Browser menu/registry card, `OpenCategory`, `OpenUtilityCard`, package focus routes, help button, Developer Tools handoff.
- Search: top toolbar search with clear.
- Filters: category sidebar or compact selector, type/status/kind/package availability filters, Developer Mode filters for hidden/internal, archived, docs coverage, status, and missing facets.
- Sorting: registry/category/type/order/display-name ordering; compact mode sorted inside draw path.
- Utility cards: compact/list cards with favorite, title, status, package availability, item kind, visibility, category chips, description, help/package/open buttons.
- Actions: action descriptors could appear as top-level cards via global kind filter or search.
- Related utilities: compact related buttons could draw on every card when enabled.
- Accessory utilities: no dedicated browser role; supporting tools competed as normal utility cards.
- Packages: other-packages tab, package search, discovery cards, package detail expansion, package developer preview and override editor.
- Developer controls: Developer Tools metadata/package/category/generation/audit workbench, metadata edit buttons, category tint controls, package overrides.
- Preferences/state: search, category/type/status, package focus, favorites, recents, view mode, filters, package preview, settings panel, sidebar width.

## After Source Mapping

- Entry points remain unchanged; `OpenUtilityCard` now also selects the card so its contextual drawer is visible.
- Search remains top-level and still finds utilities, actions, accessories, IDs, tags, package metadata, role/prominence, parent, action, accessory, and related IDs.
- Default browse surface shows core utilities first; actions and accessory utilities appear through search, explicit settings/filter toggles, Developer Mode hidden filters, or selected-card drawers.
- Actions move to selected-card drawer rows when they can be mapped by parent ID, explicit action IDs, or related utility fallback.
- Accessory utilities move to selected-card drawer rows when assigned by parent ID or explicit accessory IDs; they remain searchable and can be shown as top-level cards.
- Related utilities are contextual drawer rows/chips rather than always-full sections on every compact card.
- Sorting is explicit through `PungentUtilityBrowserSortMode`; compact/list draw paths iterate cached sorted results.
- Category counts, relationship maps, package state labels, and visible results are precomputed by `PungentUtilityBrowserViewModel`.
- Package catalog records are cached with an ID dictionary and invalidated through catalog change notifications.
- Developer Tools now includes Browser Surface metadata controls for role, priority, parent, prominence, browser tags, accessory IDs, action IDs, and validation warnings.

