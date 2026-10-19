# AI UI Design Workflow: Gemini + nanora-unity-mcp

Gemini (Nano Banana) でUI素材を生成し、nanora-unity-mcpでUnityに構築するワークフロー。

## 前提条件

- Gemini API Key（[Google AI Studio](https://aistudio.google.com/apikey)で取得）
- nanora-unity-mcp が Unity プロジェクトにインストール済み
- MCP接続済み

## ワークフロー概要

```
1. Gemini壁打ち → UIデザインアイデア（色、レイアウト、素材リスト）
2. Nano Banana画像生成 → グリーンバックでUI素材出力
3. クロマキー除去 → 透過PNG化
4. Unity MCPでインポート → AssetDatabase.Refresh
5. C#でUI構築 → Canvas, RawImage, Button配置
6. スクリーンショット → 確認・調整
```

## Step 1: Gemini でUIデザイン壁打ち

```bash
curl -s "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=$GEMINI_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
  "contents": [{"parts": [{"text": "List 6 UI assets for sci-fi game. For each: name, size, prompt for AI image gen on green background. Short prompts."}]}],
  "generationConfig": {"maxOutputTokens": 800}
}'
```

## Step 2: Nano Banana で素材生成

**モデル**: `gemini-3.1-flash-image-preview` (Nano Banana 2) または `nano-banana-pro-preview`

**重要**: 透過PNGは直接出力できない。グリーンバック(`#00FF00`)指定で生成し、後処理で除去する。

```python
import requests, base64, json

resp = requests.post(
    f"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-image-preview:generateContent?key={API_KEY}",
    json={
        "contents": [{"parts": [{"text": "Futuristic sci-fi PLAY button, glowing cyan neon edges, dark metallic, on solid bright green background #00FF00"}]}],
        "generationConfig": {"responseModalities": ["TEXT", "IMAGE"]}
    }
)
r = resp.json()
for part in r['candidates'][0]['content']['parts']:
    if 'inlineData' in part:
        img_data = base64.b64decode(part['inlineData']['data'])
        with open('btn_play.png', 'wb') as f:
            f.write(img_data)
```

### プロンプトのコツ
- `on solid bright green background #00FF00` を必ず末尾に追加
- `single element, centered, clean edges` で単体素材感を出す
- `game UI asset, high quality, digital art` でクオリティ指定
- 影や反射は除去困難なので `no shadow, no reflection` も有効

## Step 3: クロマキー除去（HSVベース）

```python
from PIL import Image
import numpy as np

def remove_green_screen(img_path, output_path):
    img = Image.open(img_path).convert("RGBA")
    data = np.array(img, dtype=np.float32)
    r, g, b = data[:,:,0], data[:,:,1], data[:,:,2]

    # HSV変換でグリーン検出
    maxc = np.maximum(np.maximum(r, g), b)
    minc = np.minimum(np.minimum(r, g), b)
    diff = maxc - minc + 1e-6
    hue = np.zeros_like(r)
    mask_g = (maxc == g)
    hue[mask_g] = (60 * ((b[mask_g] - r[mask_g]) / diff[mask_g]) + 120) % 360
    sat = diff / (maxc + 1e-6)
    val = maxc / 255.0

    # グリーン除去
    green_mask = (hue > 70) & (hue < 170) & (sat > 0.25) & (val > 0.15)
    green_strength = np.clip((g - np.maximum(r, b)) / 128.0, 0, 1)

    result = data.copy()
    result[green_mask, 3] = 0  # 完全透明
    edge_mask = ~green_mask & (green_strength > 0.1)
    result[edge_mask, 3] *= (1.0 - green_strength[edge_mask] * 0.8)

    # デスピル（緑かぶり除去）
    non_t = result[:,:,3] > 10
    g_excess = result[:,:,1] - np.maximum(result[:,:,0], result[:,:,2])
    spill = non_t & (g_excess > 10)
    result[spill, 1] = np.maximum(result[spill, 0], result[spill, 2]) + 5

    Image.fromarray(result.astype(np.uint8)).save(output_path)

remove_green_screen("btn_play.png", "btn_play_transparent.png")
```

## Step 4: Unity MCPでインポート

素材をUnityプロジェクトの`Assets/UI/`にコピーし、アセットリフレッシュ:

```json
{"name": "compile_project", "arguments": {}}
```

## Step 5: C#でUI構築

```json
{"name": "execute_csharp", "arguments": {
  "code": "var canvasObj = new GameObject(\"MenuCanvas\"); var canvas = canvasObj.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; canvasObj.AddComponent<GraphicRaycaster>(); var bgObj = new GameObject(\"Background\"); bgObj.transform.SetParent(canvasObj.transform, false); var bgImg = bgObj.AddComponent<RawImage>(); bgImg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(\"Assets/UI/bg.png\"); var bgRect = bgObj.GetComponent<RectTransform>(); bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.sizeDelta = Vector2.zero; return \"Canvas created\";"
}}
```

## Step 6: スクリーンショット確認

```json
{"name": "capture_screenshot", "arguments": {"view": "game"}}
```

> **Note (重要)**: **Screen Space Overlay Canvas と UI Toolkit の runtime panel (UIDocument) は `capture_screenshot` に映らない** — MCPのスクショはカメラRenderTexture経由で取得するため。
>
> 対処は以下のいずれか:
> - `execute_csharp` + `force_roslyn: true` で `UnityEngine.ScreenCapture.CaptureScreenshot("Temp/shot.png")` を呼び、保存ファイルをホスト側から読む
> - UGUI の Canvas は `renderMode` を `ScreenSpaceCamera` に変更し `Camera.main` を割り当てる（UGUIのみ有効。UI Toolkitには効かない）
>
> 例:
> ```json
> {"name": "execute_csharp", "arguments": {
>   "code": "UnityEngine.ScreenCapture.CaptureScreenshot(\"Temp/ui_shot.png\"); return \"ok\";",
>   "force_roslyn": true
> }}
> ```

## Gemini API Key 設定

### 取得方法
1. [Google AI Studio](https://aistudio.google.com/apikey) にアクセス
2. 「Create API Key」をクリック
3. キーをコピー

### 環境変数設定
```bash
export GEMINI_API_KEY="your-key-here"
```

### 無料枠
- Nano Banana 2: 500画像/日（無料）
- Nano Banana Pro: 50画像/日（無料）

### 注意
- 画像生成は `responseModalities: ["TEXT", "IMAGE"]` が必須
- 生成モデル名: `gemini-3.1-flash-image-preview`（Nano Banana 2）、`nano-banana-pro-preview`（Pro）
- グリーンバック指定でも完璧にグリーンにならない場合がある（プロンプト調整で改善）
