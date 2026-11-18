# EntryCategoriesApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdEntrycategoriesExportGet**](EntryCategoriesApi.md#accountsetsaccountsetidentrycategoriesexportget) | **GET** /accountsets/{accountSetId}/entrycategories/export |  |
| [**accountsetsAccountSetIdEntrycategoriesGet**](EntryCategoriesApi.md#accountsetsaccountsetidentrycategoriesget) | **GET** /accountsets/{accountSetId}/entrycategories |  |
| [**accountsetsAccountSetIdEntrycategoriesIdDelete**](EntryCategoriesApi.md#accountsetsaccountsetidentrycategoriesiddelete) | **DELETE** /accountsets/{accountSetId}/entrycategories/{id} |  |
| [**accountsetsAccountSetIdEntrycategoriesIdEnabledPatch**](EntryCategoriesApi.md#accountsetsaccountsetidentrycategoriesidenabledpatch) | **PATCH** /accountsets/{accountSetId}/entrycategories/{id}/enabled |  |
| [**accountsetsAccountSetIdEntrycategoriesIdPut**](EntryCategoriesApi.md#accountsetsaccountsetidentrycategoriesidput) | **PUT** /accountsets/{accountSetId}/entrycategories/{id} |  |
| [**accountsetsAccountSetIdEntrycategoriesImportPost**](EntryCategoriesApi.md#accountsetsaccountsetidentrycategoriesimportpost) | **POST** /accountsets/{accountSetId}/entrycategories/import |  |
| [**accountsetsAccountSetIdEntrycategoriesImportTemplateGet**](EntryCategoriesApi.md#accountsetsaccountsetidentrycategoriesimporttemplateget) | **GET** /accountsets/{accountSetId}/entrycategories/import/template |  |
| [**accountsetsAccountSetIdEntrycategoriesPost**](EntryCategoriesApi.md#accountsetsaccountsetidentrycategoriespost) | **POST** /accountsets/{accountSetId}/entrycategories |  |



## accountsetsAccountSetIdEntrycategoriesExportGet

> accountsetsAccountSetIdEntrycategoriesExportGet(accountSetId, type)



### Example

```ts
import {
  Configuration,
  EntryCategoriesApi,
} from '';
import type { AccountsetsAccountSetIdEntrycategoriesExportGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new EntryCategoriesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    type: 56,
  } satisfies AccountsetsAccountSetIdEntrycategoriesExportGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdEntrycategoriesExportGet(body);
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


## accountsetsAccountSetIdEntrycategoriesGet

> Array&lt;EntryCategoryDto&gt; accountsetsAccountSetIdEntrycategoriesGet(accountSetId)



### Example

```ts
import {
  Configuration,
  EntryCategoriesApi,
} from '';
import type { AccountsetsAccountSetIdEntrycategoriesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new EntryCategoriesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdEntrycategoriesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdEntrycategoriesGet(body);
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

[**Array&lt;EntryCategoryDto&gt;**](EntryCategoryDto.md)

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


## accountsetsAccountSetIdEntrycategoriesIdDelete

> accountsetsAccountSetIdEntrycategoriesIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  EntryCategoriesApi,
} from '';
import type { AccountsetsAccountSetIdEntrycategoriesIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new EntryCategoriesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdEntrycategoriesIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdEntrycategoriesIdDelete(body);
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


## accountsetsAccountSetIdEntrycategoriesIdEnabledPatch

> EntryCategoryDto accountsetsAccountSetIdEntrycategoriesIdEnabledPatch(accountSetId, id, setEntryCategoryEnabledRequest)



### Example

```ts
import {
  Configuration,
  EntryCategoriesApi,
} from '';
import type { AccountsetsAccountSetIdEntrycategoriesIdEnabledPatchRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new EntryCategoriesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SetEntryCategoryEnabledRequest
    setEntryCategoryEnabledRequest: ...,
  } satisfies AccountsetsAccountSetIdEntrycategoriesIdEnabledPatchRequest;

  try {
    const data = await api.accountsetsAccountSetIdEntrycategoriesIdEnabledPatch(body);
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
| **setEntryCategoryEnabledRequest** | [SetEntryCategoryEnabledRequest](SetEntryCategoryEnabledRequest.md) |  | |

### Return type

[**EntryCategoryDto**](EntryCategoryDto.md)

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


## accountsetsAccountSetIdEntrycategoriesIdPut

> EntryCategoryDto accountsetsAccountSetIdEntrycategoriesIdPut(accountSetId, id, updateEntryCategoryRequest)



### Example

```ts
import {
  Configuration,
  EntryCategoriesApi,
} from '';
import type { AccountsetsAccountSetIdEntrycategoriesIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new EntryCategoriesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // UpdateEntryCategoryRequest
    updateEntryCategoryRequest: ...,
  } satisfies AccountsetsAccountSetIdEntrycategoriesIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdEntrycategoriesIdPut(body);
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
| **updateEntryCategoryRequest** | [UpdateEntryCategoryRequest](UpdateEntryCategoryRequest.md) |  | |

### Return type

[**EntryCategoryDto**](EntryCategoryDto.md)

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


## accountsetsAccountSetIdEntrycategoriesImportPost

> EntryCategoryImportResult accountsetsAccountSetIdEntrycategoriesImportPost(accountSetId, file)



### Example

```ts
import {
  Configuration,
  EntryCategoriesApi,
} from '';
import type { AccountsetsAccountSetIdEntrycategoriesImportPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new EntryCategoriesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Blob (optional)
    file: BINARY_DATA_HERE,
  } satisfies AccountsetsAccountSetIdEntrycategoriesImportPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdEntrycategoriesImportPost(body);
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
| **file** | `Blob` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**EntryCategoryImportResult**](EntryCategoryImportResult.md)

### Authorization

No authorization required

### HTTP request headers

- **Content-Type**: `multipart/form-data`
- **Accept**: `text/plain`, `application/json`, `text/json`


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | OK |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


## accountsetsAccountSetIdEntrycategoriesImportTemplateGet

> accountsetsAccountSetIdEntrycategoriesImportTemplateGet(accountSetId)



### Example

```ts
import {
  Configuration,
  EntryCategoriesApi,
} from '';
import type { AccountsetsAccountSetIdEntrycategoriesImportTemplateGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new EntryCategoriesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdEntrycategoriesImportTemplateGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdEntrycategoriesImportTemplateGet(body);
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


## accountsetsAccountSetIdEntrycategoriesPost

> EntryCategoryDto accountsetsAccountSetIdEntrycategoriesPost(accountSetId, createEntryCategoryRequest)



### Example

```ts
import {
  Configuration,
  EntryCategoriesApi,
} from '';
import type { AccountsetsAccountSetIdEntrycategoriesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new EntryCategoriesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // CreateEntryCategoryRequest
    createEntryCategoryRequest: ...,
  } satisfies AccountsetsAccountSetIdEntrycategoriesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdEntrycategoriesPost(body);
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
| **createEntryCategoryRequest** | [CreateEntryCategoryRequest](CreateEntryCategoryRequest.md) |  | |

### Return type

[**EntryCategoryDto**](EntryCategoryDto.md)

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

