# WhereAreYouGoingGH

A **GameHelper2 plugin** for *Path of Exile 2* that helps you visualize **where enemies (and other units) are going**.  
It draws projected movement lines, endpoint markers, and optional clustering to show the direction of monsters or players in real time.

---

## ? Features

- **Motion projection**: Detects entity velocity and projects their destination based on recent movement.
- **Clustering**: Nearby monsters of the same rarity can be grouped, showing an averaged line for the whole pack.
- **Rarity-aware coloring**: Configurable colors per rarity (Normal, Magic, Rare, Unique) and other categories (Players, Self, Friendly).
- **Configurable visuals**:
  - Line thickness and circle thickness.
  - Toggle filled circles or bounding boxes.
  - Optional endpoint circle.
  - Smooth motion and screen-space jitter reduction.
- **Cluster clarity**:
  - Faint spokes connect member monsters to their cluster line.
  - Dots and optional member counts show pack sizes.

---

## ?? Settings

Open the plugin settings in GameHelper2 to configure:

### Common Settings
- **Enable**: Turn the plugin on/off.
- **Draw in Town / Hideout / Background**: Control where drawing is allowed.
- **Max draw distance**: Ignore entities too far away.
- **Projection horizon (ms)**: How far ahead to project movement.
- **Min speed to project**: Ignore idle entities.
- **Max projected screen length (px)**: Clamp overly long projections.
- **Line / Circle toggles**: Enable/disable lines, endpoints, origin circles.
- **Circle & line thickness**.
- **Velocity smoothing / Screen smoothing**: Adjust to reduce jitter.

### Grouping
- **Group by rarity**: Consolidates nearby monsters into a single averaged line.
- **Group radius (px)**: Screen-space radius for clustering.
- **Group min count**: Minimum monsters to form a cluster.
- **Show cluster members**: Draw faint spokes/dots to members.
- **Show cluster count**: Display approximate number of monsters in the cluster.

### Unit Categories
Each unit type has its own color and enable toggle:
- **Normal** (disabled by default)
- **Magic**
- **Rare**
- **Unique**
- **Players** (disabled by default)
- **Self** (disabled by default)
- **Friendly** (disabled by default)

---

## ?? Defaults

- **Normal / Magic / Rare / Unique** use rarity-colored lines.
- **Players, Self, Friendly, Normal** are disabled by default (can be enabled in settings).
- **Clustering** is enabled with a 120px radius and min count of 2.

---

## ?? Installation

1. Build the plugin or download the compiled DLL.
2. Place it into your `Plugins` folder under `GameHelper2`.
3. Launch GameHelper2 — the plugin will appear in the list as **WhereAreYouGoingGH**.

---

## ?? Usage Tips

- Increase **smoothing** if lines appear too jittery as enemies walk.
- Enable **Show Cluster Members** to make it easier to see which packs are associated with each projection line.
- If you want clearer separation between packs, reduce the **Group Radius**.
