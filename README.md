# HyperLayeringを活用したMR災害体験アプリケーション
Hyper Layering Architecture ([https://hyperlayering.org/](https://hyperlayering.org/)の概念を利用した、HoloLens 2を用いる災害体験複合現実(MR)アプリケーション。現在地の想定浸水高や土砂災害警戒区域の情報を公開されているハザードマップから取得(Hyper Layering)し、シースルーで見える現実世界に水面や岩を重ねることで、目の前の景色が災害で実際にどう変化するか確認できる。従来の防災体験XRコンテンツは、架空の場所や空間を利用しており、今立っている場所がそうなるという実感に欠ける課題がある。本アプリケーションでは、実際の位置情報と実際の災害想定規模を用いて、より現実的な体験を提供する。HoloLens 2はGNSSユニットを持たないため、位置情報はスマートフォンの専用アプリケーション([Unity Sharing Geospatial App](https://github.com/towakun887/Unity-Sharing-Geospatial-App))からHTTP通信で取得する。  
研究室のプロジェクトとして開発し、研究室が主催する[未来構想キャンプ2026 in 鳥取](https://www.keio.ac.jp/ja/sfc-pem/news/20260821-1/)内で行った高校生に向けたワークショップで、ブラッシュアップを実施。10/17・18開催の[「ぼうさいこくたい2026」](https://bosai-kokutai.jp/2026)で、[WebDINO主催ブース](https://www.webdino.org/updates/news/202610140720/)にてデモ展示を実施。[研究室のメンバーが登壇した「ぼうさいこくたい」のセッション](https://bosai-kokutai.jp/2026/s-54/)内で紹介。

### 技術情報
本ソフトウェアではCesium for Unityを利用しています。ヒエラルキー内のCeciumのコアから、以下のアクセスを持つCesiumのAPIトークンを設定してください。なお、本リポジトリ内に含まれているCesiumのAPIキーはすべて無効化済みです。
```
Access
This token provides access to the following scopes:
assets:read
With assets:read and assets:source access to the following assets:
2 - Bing Maps Aerial Ortho Imagery · Imagery
96188 - Cesium OSM Buildings 3D Tiles
1 - Cesium World Terrain Terrain · Terrain
```
