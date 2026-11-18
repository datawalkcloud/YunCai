# OperationLogsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdOperationlogsGet**](OperationLogsApi.md#accountsetsaccountsetidoperationlogsget) | **GET** /accountsets/{accountSetId}/operationlogs |  |



## accountsetsAccountSetIdOperationlogsGet

> Array&lt;OperationLogDto&gt; accountsetsAccountSetIdOperationlogsGet(accountSetId, start, end, userId, keyword)



### Example

```ts
import {
  Configuration,
  OperationLogsApi,
} from '';
import type { AccountsetsAccountSetIdOperationlogsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new OperationLogsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    start: 2013-10-20T19:20:30+01:00,
    // Date (optional)
    end: 2013-10-20T19:20:30+01:00,
    // string (optional)
    userId: userId_example,
    // string (optional)
    keyword: keyword_example,
  } satisfies AccountsetsAccountSetIdOperationlogsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdOperationlogsGet(body);
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters


| Name | Type | Description  | Notes |
|------------- | ------------- | ------------- | -------------|
| **accountSetId** | `string` |  | [Defaults to `undefined`] |
| **start** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **end** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **userId** | `string` |  | [Optional] [Defaults to `undefined`] |
| **keyword** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;OperationLogDto&gt;**](OperationLogDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)

