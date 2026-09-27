# Third-Party Notices

Hiker is distributed under the MIT License (see `LICENSE`). It bundles or
depends on the following third-party components. Each remains subject to its
own license terms, reproduced or referenced below.

| Component | Version | License |
|-----------|---------|---------|
| CommunityToolkit.Maui | 9.1.0 | MIT |
| CommunityToolkit.Mvvm | 8.3.2 | MIT |
| CsvHelper | 33.0.1 | MS-PL OR Apache-2.0 (dual-licensed) |
| Microsoft.Maui.Controls | 9.0.21 | MIT |
| Microsoft.Maui.Essentials | 9.0.21 | MIT |
| Microsoft.Extensions.Logging.Debug | 9.0.0 | MIT |
| System.Text.Json | 9.0.0 | MIT |
| MapLibre GL JS (bundled in `Resources/Raw`) | 4.7.1 | BSD-3-Clause |

## Additional acknowledgements

- Map rendering: [MapLibre GL JS](https://maplibre.org/) 4.7.1, BSD-3-Clause
  license. It is bundled inside the app (`Resources/Raw/maplibre-gl.js` and
  `maplibre-gl.css`); the full license text ships next to it in
  `Resources/Raw/maplibre-LICENSE.txt`.
- Map style and vector tiles: [OpenFreeMap](https://openfreemap.org/)
  ("liberty" style), used without an API key.
- Map data: © OpenStreetMap contributors, available under the Open Database
  License (ODbL). The same data is queried through the Overpass API when a
  recorded route is adjusted to the paths on the map.

---

The MIT License text that applies to the MIT-licensed components above is the
same as the one in the `LICENSE` file at the root of this repository.

CsvHelper is dual-licensed and may be used under either the Microsoft Public
License (MS-PL) or the Apache License 2.0, at your option. See
<https://github.com/JoshClose/CsvHelper/blob/master/LICENSE.txt> for details.
