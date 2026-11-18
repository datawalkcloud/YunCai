# AssistItemsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdAssistItemsCategoriesGet**](AssistItemsApi.md#accountsetsaccountsetidassistitemscategoriesget) | **GET** /accountsets/{accountSetId}/AssistItems/categories |  |
| [**accountsetsAccountSetIdAssistItemsChildrenGet**](AssistItemsApi.md#accountsetsaccountsetidassistitemschildrenget) | **GET** /accountsets/{accountSetId}/AssistItems/children |  |
| [**accountsetsAccountSetIdAssistItemsGet**](AssistItemsApi.md#accountsetsaccountsetidassistitemsget) | **GET** /accountsets/{accountSetId}/AssistItems |  |
| [**accountsetsAccountSetIdAssistItemsIdDelete**](AssistItemsApi.md#accountsetsaccountsetidassistitemsiddelete) | **DELETE** /accountsets/{accountSetId}/AssistItems/{id} |  |
| [**accountsetsAccountSetIdAssistItemsIdEnabledPatch**](AssistItemsApi.md#accountsetsaccountsetidassistitemsidenabledpatch) | **PATCH** /accountsets/{accountSetId}/AssistItems/{id}/enabled |  |
| [**accountsetsAccountSetIdAssistItemsIdPut**](AssistItemsApi.md#accountsetsaccountsetidassistitemsidput) | **PUT** /accountsets/{accountSetId}/AssistItems/{id} |  |
| [**accountsetsAccountSetIdAssistItemsNextCodeGet**](AssistItemsApi.md#accountsetsaccountsetidassistitemsnextcodeget) | **GET** /accountsets/{accountSetId}/AssistItems/next-code |  |
| [**accountsetsAccountSetIdAssistItemsPost**](AssistItemsApi.md#accountsetsaccountsetidassistitemspost) | **POST** /accountsets/{accountSetId}/AssistItems |  |



## accountsetsAccountSetIdAssistItemsCategoriesGet

> Array&lt;AssistCategoryStatDto&gt; accountsetsAccountSetIdAssistItemsCategoriesGet(accountSetId)



### Example

```ts
import {
  Configuration,
  AssistItemsApi,
} from '';
import type { AccountsetsAccountSetIdAssistItemsCategoriesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AssistItemsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAssistItemsCategoriesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAssistItemsCategoriesGet(body);
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

[**Array&lt;AssistCategoryStatDto&gt;**](AssistCategoryStatDto.md)

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


## accountsetsAccountSetIdAssistItemsChildrenGet

> Array&lt;AssistItemDto&gt; accountsetsAccountSetIdAssistItemsChildrenGet(accountSetId, type, parentId)



### Example

```ts
import {
  Configuration,
  AssistItemsApi,
} from '';
import type { AccountsetsAccountSetIdAssistItemsChildrenGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AssistItemsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    type: 56,
    // string (optional)
    parentId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAssistItemsChildrenGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAssistItemsChildrenGet(body);
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
| **type** | `number` |  | [Optional] [Defaults to `undefined`] |
| **parentId** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;AssistItemDto&gt;**](AssistItemDto.md)

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


## accountsetsAccountSetIdAssistItemsGet

> Array&lt;AssistItemDto&gt; accountsetsAccountSetIdAssistItemsGet(accountSetId, type)



### Example

```ts
import {
  Configuration,
  AssistItemsApi,
} from '';
import type { AccountsetsAccountSetIdAssistItemsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AssistItemsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    type: 56,
  } satisfies AccountsetsAccountSetIdAssistItemsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAssistItemsGet(body);
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
| **type** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;AssistItemDto&gt;**](AssistItemDto.md)

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


## accountsetsAccountSetIdAssistItemsIdDelete

> accountsetsAccountSetIdAssistItemsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  AssistItemsApi,
} from '';
import type { AccountsetsAccountSetIdAssistItemsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AssistItemsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAssistItemsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdAssistItemsIdDelete(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |

### Return type

`void` (Empty response body)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: Not defined
- **Accept**: Not defined


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdAssistItemsIdEnabledPatch

> AssistItemDto accountsetsAccountSetIdAssistItemsIdEnabledPatch(accountSetId, id, setAssistItemEnabledRequest)



### Example

```ts
import {
  Configuration,
  AssistItemsApi,
} from '';
import type { AccountsetsAccountSetIdAssistItemsIdEnabledPatchRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AssistItemsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SetAssistItemEnabledRequest
    setAssistItemEnabledRequest: ...,
  } satisfies AccountsetsAccountSetIdAssistItemsIdEnabledPatchRequest;

  try {
    const data = await api.accountsetsAccountSetIdAssistItemsIdEnabledPatch(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |
| **setAssistItemEnabledRequest** | [SetAssistItemEnabledRequest](SetAssistItemEnabledRequest.md) |  | |

### Return type

[**AssistItemDto**](AssistItemDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdAssistItemsIdPut

> AssistItemDto accountsetsAccountSetIdAssistItemsIdPut(accountSetId, id, updateAssistItemRequest)



### Example

```ts
import {
  Configuration,
  AssistItemsApi,
} from '';
import type { AccountsetsAccountSetIdAssistItemsIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AssistItemsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // UpdateAssistItemRequest
    updateAssistItemRequest: ...,
  } satisfies AccountsetsAccountSetIdAssistItemsIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdAssistItemsIdPut(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |
| **updateAssistItemRequest** | [UpdateAssistItemRequest](UpdateAssistItemRequest.md) |  | |

### Return type

[**AssistItemDto**](AssistItemDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdAssistItemsNextCodeGet

> string accountsetsAccountSetIdAssistItemsNextCodeGet(accountSetId, type, parentId)



### Example

```ts
import {
  Configuration,
  AssistItemsApi,
} from '';
import type { AccountsetsAccountSetIdAssistItemsNextCodeGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AssistItemsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    type: 56,
    // string (optional)
    parentId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdAssistItemsNextCodeGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdAssistItemsNextCodeGet(body);
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
| **type** | `number` |  | [Optional] [Defaults to `undefined`] |
| **parentId** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

**string**

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


## accountsetsAccountSetIdAssistItemsPost

> AssistItemDto accountsetsAccountSetIdAssistItemsPost(accountSetId, createAssistItemRequest)



### Example

```ts
import {
  Configuration,
  AssistItemsApi,
} from '';
import type { AccountsetsAccountSetIdAssistItemsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AssistItemsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // CreateAssistItemRequest
    createAssistItemRequest: ...,
  } satisfies AccountsetsAccountSetIdAssistItemsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdAssistItemsPost(body);
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
| **createAssistItemRequest** | [CreateAssistItemRequest](CreateAssistItemRequest.md) |  | |

### Return type

[**AssistItemDto**](AssistItemDto.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `application/json`, `text/json`, `application/*+json`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)

