# AeroSim 0.4 UI 比例修复 QA

原生缩放设置测试，隔离用户数据。

```json
{
  "version": "0.4.0",
  "date": "2026-10-07",
  "mode": "Native UI/HUD/Text preferences, isolated profile and app copy",
  "totals": {
    "runs": 13,
    "states": 1718,
    "checks": 197705,
    "runtimeErrors": 0,
    "geometryFailures": 0,
    "designFailures": 0,
    "textCandidates": 8551,
    "interactionFailures": 0,
    "regressionFailures": 0
  },
  "rules": {
    "regression.ui-scale-immediate": {
      "checks": 13,
      "failed": 0
    },
    "regression.scales-persist": {
      "checks": 13,
      "failed": 0
    },
    "regression.hud-scale-independent": {
      "checks": 13,
      "failed": 0
    },
    "regression.menu-scale-restored": {
      "checks": 13,
      "failed": 0
    },
    "viewport.panel-scale": {
      "checks": 1650,
      "failed": 0
    },
    "design.minimum-font-12": {
      "checks": 62626,
      "failed": 0
    },
    "layout.text-fits": {
      "checks": 52761,
      "failed": 8551
    },
    "layout.horizontal-containment": {
      "checks": 8030,
      "failed": 0
    },
    "design.action-height-44": {
      "checks": 8030,
      "failed": 0
    },
    "layout.sibling-buttons-no-overlap": {
      "checks": 44275,
      "failed": 0
    },
    "design.topbar-height-64": {
      "checks": 1375,
      "failed": 0
    },
    "design.nav-width": {
      "checks": 1375,
      "failed": 0
    },
    "layout.critical-action-reachable": {
      "checks": 3300,
      "failed": 0
    },
    "regression.six-aircraft": {
      "checks": 55,
      "failed": 0
    },
    "design.user-ui-scale-setting": {
      "checks": 55,
      "failed": 0
    },
    "design.independent-hud-scale-setting": {
      "checks": 55,
      "failed": 0
    },
    "design.text-200-user-setting": {
      "checks": 55,
      "failed": 0
    },
    "interaction.launch": {
      "checks": 55,
      "failed": 0
    },
    "layout.hud-contained": {
      "checks": 1828,
      "failed": 0
    },
    "layout.hud-content-contained": {
      "checks": 1760,
      "failed": 0
    },
    "design.hud-central-view-clear": {
      "checks": 1388,
      "failed": 0
    },
    "layout.hud-blocks-no-overlap": {
      "checks": 6800,
      "failed": 0
    },
    "design.hud-top-max-48": {
      "checks": 220,
      "failed": 0
    },
    "design.hud-bottom-max-56": {
      "checks": 220,
      "failed": 0
    },
    "layout.pause-panel-contained": {
      "checks": 55,
      "failed": 0
    },
    "layout.pause-action-visible": {
      "checks": 275,
      "failed": 0
    },
    "interaction.pause": {
      "checks": 55,
      "failed": 0
    },
    "interaction.resume": {
      "checks": 55,
      "failed": 0
    },
    "layout.osd-token-contained": {
      "checks": 220,
      "failed": 0
    },
    "layout.osd-tokens-no-overlap": {
      "checks": 330,
      "failed": 0
    },
    "design.content-max-1600": {
      "checks": 750,
      "failed": 0
    }
  },
  "runs": [
    {
      "run": "desktop-1280x720",
      "width": 1280,
      "height": 720,
      "states": 218,
      "runtimeErrors": 0
    },
    {
      "run": "desktop-1680x945",
      "width": 1680,
      "height": 946,
      "states": 187,
      "runtimeErrors": 0
    },
    {
      "run": "desktop-1920x1080",
      "width": 1920,
      "height": 1080,
      "states": 218,
      "runtimeErrors": 0
    },
    {
      "run": "desktop-2560x1440",
      "width": 2560,
      "height": 1440,
      "states": 187,
      "runtimeErrors": 0
    },
    {
      "run": "desktop-3840x2160",
      "width": 3840,
      "height": 1992,
      "states": 187,
      "runtimeErrors": 0
    },
    {
      "run": "desktop-4k-fullscreen",
      "width": 3840,
      "height": 2160,
      "states": 187,
      "runtimeErrors": 0
    },
    {
      "run": "ultrawide-2520x1080",
      "width": 2520,
      "height": 1080,
      "states": 187,
      "runtimeErrors": 0
    },
    {
      "run": "acceptance-2560x1080",
      "width": 2560,
      "height": 1080,
      "states": 187,
      "runtimeErrors": 0
    },
    {
      "run": "tablet-1024x768",
      "width": 1024,
      "height": 768,
      "states": 32,
      "runtimeErrors": 0
    },
    {
      "run": "phone-landscape-844x390",
      "width": 844,
      "height": 390,
      "states": 32,
      "runtimeErrors": 0
    },
    {
      "run": "phone-portrait-390x844",
      "width": 390,
      "height": 844,
      "states": 32,
      "runtimeErrors": 0
    },
    {
      "run": "dpi2-2048x1536",
      "width": 2048,
      "height": 1536,
      "states": 32,
      "runtimeErrors": 0
    },
    {
      "run": "dpi3-2532x1170",
      "width": 2532,
      "height": 1170,
      "states": 32,
      "runtimeErrors": 0
    }
  ],
  "missing": [],
  "smoke": {
    "passed": 120,
    "failed": 0,
    "available": true
  },
  "limitations": [
    "Desktop macOS native tests only; tablet/phone/DPI cases are logical viewport probes, not iOS hardware tests.",
    "Text measurements are candidates requiring screenshot review, not a pixel-perfect visual score.",
    "Pause actions may require scrolling; viewport containment and each scrolled action are checked.",
    "This fixes proportions, not all page information architecture differences from concept art.",
    "Full-page 200% text stress is tested at 100% UI/HUD scale on 1280 and 1920 desktop widths; all mixed maximum-zoom/text combinations are not covered."
  ],
  "evidenceErrors": [],
  "build": {
    "runtimeSha256": "d4e8ddb4bf523a2e6eda2f0254b9f389ea242a6c7c1989c9841a7752ab52f69b",
    "resourcesSha256": "47ceb2e68a112f7ae8b79d20cd226b190fb0bc32debba80cf4e48f6f60a62b1c",
    "responsiveSourceSha256": "81f788f87eed1033063e5c7e2b3b3fe15330cfc400b756a67a4b4fc6f534c6ee"
  }
}
```

全部实际截图见根目录 AeroSim-0.4-UI比例修复.html。文字候选需人工视觉检查；不代表全设计逐像素一致或 iOS 真机验证。
