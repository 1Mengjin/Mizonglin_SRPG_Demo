# 字体许可

浏览器没有可用的系统字体，WebGL 构建必须把字体打进包内，因此这里只放**可自由分发**的字体子集，并用 `pyftsubset` 裁剪到本项目实际用到的字符（794 个用字 + ASCII 与常用中英文标点）。原始全字库不随仓库分发。

| 文件 | 字体 | 用途 | 许可 |
|---|---|---|---|
| `SourceHanSansSC-Regular.otf` | 思源黑体 SC Regular（Source Han Sans SC） | 界面正文、名字板、按钮 | SIL Open Font License 1.1，Copyright 2014-2021 Adobe (http://www.adobe.com/)，Reserved Font Name "Source" |
| `YanShiXiaXingKai.ttf` | 演示夏行楷（内部名 Slidexiaxing） | 回合徽章、胜利条件横幅、技能横框 | 作者 Keynote 人生哥，免费商用；条款允许自由修改，但**不可二次贩卖字体文件** |

原始字库下载地址：

- 思源黑体 SC：<https://github.com/adobe-fonts/source-han-sans>
- 演示夏行楷：<https://github.com/maoken-fonts/slidefont>

两个字体都以子集形式使用，未修改笔画形状或 Font Name，符合各自许可对「修改版」的限制。

## 重建子集

新增文案出现子集外的字时，用下面命令重建（`charset.txt` 由 `Assets` 下全部 `.cs` / `.unity` / `.asset` / `.json` / `.txt` 文本提取而来）：

```powershell
python -m fontTools.subset <原字体> --text-file=charset.txt --output-file=<子集输出> --layout-features='*' --name-IDs='*' --name-languages='*' --name-legacy --glyph-names --symbol-cmap --legacy-cmap
```

注意：行楷字库缺少 `· × → ↓ ◇ ● ✓` 等符号，这些字符只出现在正文里，不能用于横幅文案。
