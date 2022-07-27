﻿using System;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
#if WINDOWS_PHONE
[assembly: AssemblyTitle("Simple.OData.Client (Windows Phone)")]
#elif SILVERLIGHT
[assembly: AssemblyTitle("Simple.OData.Client (Silverlight)")]
#elif PocketPC
[assembly: AssemblyTitle("Simple.OData.Client (Compact)")]
#elif PORTABLE
[assembly: AssemblyTitle("Simple.OData.Client (Portable)")]
#elif NETFX_CORE
[assembly: AssemblyTitle("Simple.OData.Client (WinRT)")]
#elif NET20
[assembly: AssemblyTitle("Simple.OData.Client (NET 2.0)")]
#elif NET35
[assembly: AssemblyTitle("Simple.OData.Client (NET 3.5)")]
#elif NET40
[assembly: AssemblyTitle("Simple.OData.Client (NET 4.0)")]
#else
[assembly: AssemblyTitle("Simple.OData.Client")]
#endif

[assembly: AssemblyDescription("OData client library for .NET 4.x, Windows Store, Silverlight 5, Windows Phone 8, Mond for Android and MonoTouch platforms")]


[assembly: InternalsVisibleTo("Simple.OData.Client.Dynamic, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.V3.Adapter, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.V4.Adapter, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.Tests, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.IntegrationTests, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.Tests.Core, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.Tests.Net40, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.Tests.Net45, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.Tests.WinRT, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.Tests.SL5, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.Tests.WP8, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.Tests.Droid, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]
[assembly: InternalsVisibleTo("Simple.OData.Client.Tests.Touch, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d608f5bce35f3eff1c1102fc3a90c0c1fb48a69491aa396dc6f2b3214374645179700266ff7c64b35de02956afd8e0f29a6de4d4a32660d2ac7c531901daf5e1130944b1ca1e9a95cb7abcadca5aab49507e5673c1d0315e41157c0daf720fca9a7bfa78a264648bedc83ddb75792c607dda0b84e48ff577db2898566a470c2")]