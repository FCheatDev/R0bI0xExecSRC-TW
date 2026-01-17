# ScriptBlox API Reference

## Search Scripts

Search ScriptBlox scripts with a keyword plus optional filters.

### API Path

`/api/script/search`

### Parameters

| Parameter | Description                     | Required | Type                                                                     | Default   |
| --------- | ------------------------------- | -------- | ------------------------------------------------------------------------ | --------- |
| q         | Search query                    | ✅       | string                                                                   | -         |
| page      | Page number (pagination)        | ❌       | number                                                                   | 1         |
| max       | Max scripts per page (1–20)     | ❌       | number                                                                   | 20        |
| mode      | Script type                     | ❌       | free \| paid                                                             | -         |
| patched   | Whether script is patched       | ❌       | 1 (yes) \| 0 (no)                                                        | -         |
| key       | Whether script has a key system | ❌       | 1 (yes) \| 0 (no)                                                        | -         |
| universal | Whether script is universal     | ❌       | 1 (yes) \| 0 (no)                                                        | -         |
| verified  | Whether script is verified      | ❌       | 1 (yes) \| 0 (no)                                                        | -         |
| sortBy    | Sort criteria                   | ❌       | views \| likeCount \| createdAt \| updatedAt \| dislikeCount \| accuracy | updatedAt |
| order     | Sort order                      | ❌       | asc \| desc                                                              | desc      |
| strict    | Strict search                   | ❌       | true \| false                                                            | true      |
| owner     | Filter by username              | ❌       | string                                                                   | -         |
| placeId   | Filter by game ID               | ❌       | number                                                                   | -         |

### Response

```json
{
  "result": {
    "totalPages": 0,
    "scripts": [
      {
        "_id": "string",
        "title": "string",
        "game": {
          "_id": "string",
          "name": "string",
          "imageUrl": "string"
        },
        "slug": "string",
        "verified": true,
        "key": false,
        "views": 0,
        "scriptType": "string",
        "isUniversal": false,
        "isPatched": false,
        "createdAt": "string",
        "updatedAt": "string",
        "image": "string",
        "script": "string",
        "matched": ["string"]
      }
    ]
  }
}
```

### Error Response

```json
{
  "message": "string"
}
```

### Usage (C#)

```csharp
using System.Text.Json;
using System.Net.Http.Json;

public async Task FetchScripts()
{
    using var client = new HttpClient();
    try
    {
        JsonElement scripts = await client.GetFromJsonAsync<JsonElement>(
            "https://scriptblox.com/api/script/search?q=admin");

        foreach (JsonElement script in scripts.GetProperty("result").GetProperty("scripts"))
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ScriptPanel.Children.Add(new TextBlock
                {
                    Text = $"Title: {script.GetProperty("title").GetString()}\nSlug: {script.GetProperty("slug").GetString()}"
                });
            });
        }
    }
    catch (HttpRequestException e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ScriptPanel.Children.Add(new TextBlock
            {
                Text = $"Network error while fetching scripts:\n{e.Message}",
                Margin = new Thickness(10)
            });
        });
    }
    catch (JsonException e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ScriptPanel.Children.Add(new TextBlock
            {
                Text = $"Error parsing JSON response:\n{e.Message}",
                Margin = new Thickness(10)
            });
        });
    }
    catch (Exception e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ScriptPanel.Children.Add(new TextBlock
            {
                Text = $"Unexpected error:\n{e.Message}",
                Margin = new Thickness(10)
            });
        });
    }
}
```

---

## Fetch Scripts

Fetch home page scripts or fetch with filters.

### API Path

`/api/script/fetch`

### Parameters

| Parameter | Description                     | Required | Type                                                         | Default   |
| --------- | ------------------------------- | -------- | ------------------------------------------------------------ | --------- |
| page      | Page number (pagination)        | ❌       | number                                                       | 1         |
| max       | Max scripts per page (1–20)     | ❌       | number                                                       | 20        |
| exclude   | Exclude a script ID             | ❌       | string                                                       | -         |
| mode      | Script type                     | ❌       | free \| paid                                                 | -         |
| patched   | Whether script is patched       | ❌       | 1 (yes) \| 0 (no)                                            | -         |
| key       | Whether script has a key system | ❌       | 1 (yes) \| 0 (no)                                            | -         |
| universal | Whether script is universal     | ❌       | 1 (yes) \| 0 (no)                                            | -         |
| verified  | Whether script is verified      | ❌       | 1 (yes) \| 0 (no)                                            | -         |
| sortBy    | Sort criteria                   | ❌       | views \| likeCount \| createdAt \| updatedAt \| dislikeCount | updatedAt |
| order     | Sort order                      | ❌       | asc \| desc                                                  | desc      |
| owner     | Filter by username              | ❌       | string                                                       | -         |
| placeId   | Filter by game ID               | ❌       | number                                                       | -         |

### Response

```json
{
  "result": {
    "totalPages": 0,
    "nextPage": 0,
    "max": 0,
    "scripts": [
      {
        "_id": "string",
        "title": "string",
        "game": {
          "_id": "string",
          "name": "string",
          "imageUrl": "string"
        },
        "slug": "string",
        "verified": true,
        "key": false,
        "views": 0,
        "scriptType": "string",
        "isUniversal": false,
        "isPatched": false,
        "image": "string",
        "createdAt": "string",
        "script": "string"
      }
    ]
  }
}
```

### Error Response

```json
{
  "message": "string"
}
```

### Usage (C#)

```csharp
using System.Text.Json;
using System.Net.Http.Json;

public async Task FetchScripts()
{
    using var client = new HttpClient();
    try
    {
        JsonElement scripts = await client.GetFromJsonAsync<JsonElement>(
            "https://scriptblox.com/api/script/fetch");

        foreach (JsonElement script in scripts.GetProperty("result").GetProperty("scripts"))
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ScriptPanel.Children.Add(new TextBlock
                {
                    Text = $"Title: {script.GetProperty("title").GetString()}\nSlug: {script.GetProperty("slug").GetString()}"
                });
            });
        }
    }
    catch (HttpRequestException e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ScriptPanel.Children.Add(new TextBlock
            {
                Text = $"Network error while fetching scripts:\n{e.Message}",
                Margin = new Thickness(10)
            });
        });
    }
    catch (JsonException e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ScriptPanel.Children.Add(new TextBlock
            {
                Text = $"Error parsing JSON response:\n{e.Message}",
                Margin = new Thickness(10)
            });
        });
    }
    catch (Exception e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ScriptPanel.Children.Add(new TextBlock
            {
                Text = $"Unexpected error:\n{e.Message}",
                Margin = new Thickness(10)
            });
        });
    }
}
```
