# 字体许可

WebGL 构建需要把字体打进包内（浏览器没有可用的系统字体），因此这里只放可自由分发的开源字体子集，并用 `pyftsubset` 裁剪到本项目实际用到的字符。

| 文件 | 字体 | 许可 |
|---|---|---|
| `SourceHanSansSC-Regular.otf` | 思源黑体 SC Regular（Source Han Sans SC） | SIL Open Font License 1.1，Copyright 2014-2021 Adobe (http://www.adobe.com/)，Reserved Font Name "Source" |
| `NotoSansSC-Bold.ttf` | Noto Sans SC Bold | SIL Open Font License 1.1，Copyright 2015 Google Inc. |

两处字体都以子集形式使用，未修改笔画形状，符合 OFL 对「修改版保留原名称」的要求；本仓库未重命名 Font Name。

字体子集包含的字符集来源：`Assets` 下全部 `.cs` / `.unity` / `.asset` / `.json` 文本中出现的字符，加上 ASCII 与常用中英文标点。若新增文案出现子集外的字，会用下面命令重建子集：

```powershell
python -m fontTools.subset <原字体> --text-file=charset.txt --output-file=<子集输出> --layout-features='*' --name-IDs='*' --name-languages='*' --name-legacy --glyph-names --symbol-cmap --legacy-cmap
```

**不在此目录的文件**：`Assets/Fonts/SanJiXingKaiJianTi-Cu-2.ttf`（三极字库行楷）仅有平面设计用途的商用授权，其条款明确排除「嵌入式用途」，因此不参与构建，且已在 `.gitignore` 中排除，不随仓库分发。
