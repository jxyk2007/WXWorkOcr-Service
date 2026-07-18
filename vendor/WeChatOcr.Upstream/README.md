# [WeChatOcr](https://github.com/ZGGSONG/WeChatOcr/)

[![Language](https://img.shields.io/badge/language-C%23-blue.svg?style=flat-square)](https://github.com/ZGGSONG/WeChatOcr/search?l=C%23&o=desc&s=&type=Code)

Description

### Nuget

[![NuGet](https://img.shields.io/nuget/dt/WeChatOcr.svg?style=flat-square&label=WeChatOcr)](https://www.nuget.org/packages/WeChatOcr/)

```
Install-Package WeChatOcr
```

### Usage

- [WeChatOcr](./src/WeChatOcr/README.md)
- Example Code

```cs
//1. image file
var imgPath = @"C:\Users\admin\Desktop\test.png";	//Your image file path
//2. image bytes
var imgBytes = new Byte[] {};	//Your image binary data
using var ocr = new ImageOcr();	//You can use the built-in WeChat on your computer for OCR.
ocr.Run(imgPath,	//imgPath or imgBytes
(path, result) =>
{
	try
	{
		if (result == null) return;
		var list = result?.OcrResult?.SingleResult;
		if (list == null)
		{
			//避免重复触发
			_tcs.SetResult("WeChatOCR get result is null");
			return;
		}

		var sb = new StringBuilder();
		for (var i = 0; i < list?.Count; i++)
		{
			if (list[i] is not { } item || string.IsNullOrEmpty(item.SingleStrUtf8))
				continue;

			sb.AppendLine(item.SingleStrUtf8);
		}

		_tcs.SetResult(sb.ToString());
	}
	catch (Exception ex)
	{
		System.Diagnostics.Debug.WriteLine(ex.Message);
	}
});
```


### presentation

Study only, do not use for commercial purposes

### Thanks

- [https://github.com/EEEEhex/QQImpl/](https://github.com/EEEEhex/QQImpl/)
- [https://github.com/lanni1981/WeChatOCR_CSharp](https://github.com/lanni1981/WeChatOCR_CSharp)
- [https://aardio.online/thread-449.htm](https://aardio.online/thread-449.htm)