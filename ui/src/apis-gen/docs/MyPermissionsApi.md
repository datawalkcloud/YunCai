# MyPermissionsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdMyPermissionsGet**](MyPermissionsApi.md#accountsetsaccountsetidmypermissionsget) | **GET** /accountsets/{accountSetId}/my-permissions |  |



## accountsetsAccountSetIdMyPermissionsGet

> MyPermissionsDto accountsetsAccountSetIdMyPermissionsGet(accountSetId)



### Example

```ts
import {
  Configuration,
  MyPermissionsApi,
} from '';
import type { AccountsetsAccountSetIdMyPermissionsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new MyPermissionsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdMyPermissionsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdMyPermissionsGet(body);
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

### Return type

[**MyPermissionsDto**](MyPermissionsDto.md)

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

