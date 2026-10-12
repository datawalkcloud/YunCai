# FixedAssetsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdFixedassetsAssetsGet**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetsassetsget) | **GET** /accountsets/{accountSetId}/fixedassets/assets |  |
| [**accountsetsAccountSetIdFixedassetsAssetsIdClearPost**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetsassetsidclearpost) | **POST** /accountsets/{accountSetId}/fixedassets/assets/{id}/clear |  |
| [**accountsetsAccountSetIdFixedassetsAssetsIdDelete**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetsassetsiddelete) | **DELETE** /accountsets/{accountSetId}/fixedassets/assets/{id} |  |
| [**accountsetsAccountSetIdFixedassetsAssetsIdPut**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetsassetsidput) | **PUT** /accountsets/{accountSetId}/fixedassets/assets/{id} |  |
| [**accountsetsAccountSetIdFixedassetsAssetsPost**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetsassetspost) | **POST** /accountsets/{accountSetId}/fixedassets/assets |  |
| [**accountsetsAccountSetIdFixedassetsCategoriesGet**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetscategoriesget) | **GET** /accountsets/{accountSetId}/fixedassets/categories |  |
| [**accountsetsAccountSetIdFixedassetsCategoriesIdDelete**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetscategoriesiddelete) | **DELETE** /accountsets/{accountSetId}/fixedassets/categories/{id} |  |
| [**accountsetsAccountSetIdFixedassetsCategoriesIdPut**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetscategoriesidput) | **PUT** /accountsets/{accountSetId}/fixedassets/categories/{id} |  |
| [**accountsetsAccountSetIdFixedassetsCategoriesPost**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetscategoriespost) | **POST** /accountsets/{accountSetId}/fixedassets/categories |  |
| [**accountsetsAccountSetIdFixedassetsChangesGet**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetschangesget) | **GET** /accountsets/{accountSetId}/fixedassets/changes |  |
| [**accountsetsAccountSetIdFixedassetsCheckGet**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetscheckget) | **GET** /accountsets/{accountSetId}/fixedassets/check |  |
| [**accountsetsAccountSetIdFixedassetsDepreciatePost**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetsdepreciatepost) | **POST** /accountsets/{accountSetId}/fixedassets/depreciate |  |
| [**accountsetsAccountSetIdFixedassetsDepreciationLedgerGet**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetsdepreciationledgerget) | **GET** /accountsets/{accountSetId}/fixedassets/depreciation-ledger |  |
| [**accountsetsAccountSetIdFixedassetsSummaryGet**](FixedAssetsApi.md#accountsetsaccountsetidfixedassetssummaryget) | **GET** /accountsets/{accountSetId}/fixedassets/summary |  |



## accountsetsAccountSetIdFixedassetsAssetsGet

> Array&lt;FixedAssetDto&gt; accountsetsAccountSetIdFixedassetsAssetsGet(accountSetId)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsAssetsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdFixedassetsAssetsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsAssetsGet(body);
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

[**Array&lt;FixedAssetDto&gt;**](FixedAssetDto.md)

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


## accountsetsAccountSetIdFixedassetsAssetsIdClearPost

> FixedAssetDto accountsetsAccountSetIdFixedassetsAssetsIdClearPost(accountSetId, id, clearAssetRequest)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsAssetsIdClearPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ClearAssetRequest
    clearAssetRequest: ...,
  } satisfies AccountsetsAccountSetIdFixedassetsAssetsIdClearPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsAssetsIdClearPost(body);
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
| **clearAssetRequest** | [ClearAssetRequest](ClearAssetRequest.md) |  | |

### Return type

[**FixedAssetDto**](FixedAssetDto.md)

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


## accountsetsAccountSetIdFixedassetsAssetsIdDelete

> accountsetsAccountSetIdFixedassetsAssetsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsAssetsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdFixedassetsAssetsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsAssetsIdDelete(body);
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


## accountsetsAccountSetIdFixedassetsAssetsIdPut

> FixedAssetDto accountsetsAccountSetIdFixedassetsAssetsIdPut(accountSetId, id, saveFixedAssetRequest)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsAssetsIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveFixedAssetRequest
    saveFixedAssetRequest: ...,
  } satisfies AccountsetsAccountSetIdFixedassetsAssetsIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsAssetsIdPut(body);
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
| **saveFixedAssetRequest** | [SaveFixedAssetRequest](SaveFixedAssetRequest.md) |  | |

### Return type

[**FixedAssetDto**](FixedAssetDto.md)

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


## accountsetsAccountSetIdFixedassetsAssetsPost

> FixedAssetDto accountsetsAccountSetIdFixedassetsAssetsPost(accountSetId, saveFixedAssetRequest)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsAssetsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveFixedAssetRequest
    saveFixedAssetRequest: ...,
  } satisfies AccountsetsAccountSetIdFixedassetsAssetsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsAssetsPost(body);
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
| **saveFixedAssetRequest** | [SaveFixedAssetRequest](SaveFixedAssetRequest.md) |  | |

### Return type

[**FixedAssetDto**](FixedAssetDto.md)

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


## accountsetsAccountSetIdFixedassetsCategoriesGet

> Array&lt;FixedAssetCategoryDto&gt; accountsetsAccountSetIdFixedassetsCategoriesGet(accountSetId)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsCategoriesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdFixedassetsCategoriesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsCategoriesGet(body);
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

[**Array&lt;FixedAssetCategoryDto&gt;**](FixedAssetCategoryDto.md)

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


## accountsetsAccountSetIdFixedassetsCategoriesIdDelete

> accountsetsAccountSetIdFixedassetsCategoriesIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsCategoriesIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdFixedassetsCategoriesIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsCategoriesIdDelete(body);
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


## accountsetsAccountSetIdFixedassetsCategoriesIdPut

> FixedAssetCategoryDto accountsetsAccountSetIdFixedassetsCategoriesIdPut(accountSetId, id, saveFixedAssetCategoryRequest)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsCategoriesIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveFixedAssetCategoryRequest
    saveFixedAssetCategoryRequest: ...,
  } satisfies AccountsetsAccountSetIdFixedassetsCategoriesIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsCategoriesIdPut(body);
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
| **saveFixedAssetCategoryRequest** | [SaveFixedAssetCategoryRequest](SaveFixedAssetCategoryRequest.md) |  | |

### Return type

[**FixedAssetCategoryDto**](FixedAssetCategoryDto.md)

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


## accountsetsAccountSetIdFixedassetsCategoriesPost

> FixedAssetCategoryDto accountsetsAccountSetIdFixedassetsCategoriesPost(accountSetId, saveFixedAssetCategoryRequest)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsCategoriesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveFixedAssetCategoryRequest
    saveFixedAssetCategoryRequest: ...,
  } satisfies AccountsetsAccountSetIdFixedassetsCategoriesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsCategoriesPost(body);
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
| **saveFixedAssetCategoryRequest** | [SaveFixedAssetCategoryRequest](SaveFixedAssetCategoryRequest.md) |  | |

### Return type

[**FixedAssetCategoryDto**](FixedAssetCategoryDto.md)

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


## accountsetsAccountSetIdFixedassetsChangesGet

> Array&lt;FixedAssetChangeDto&gt; accountsetsAccountSetIdFixedassetsChangesGet(accountSetId, assetId)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsChangesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string (optional)
    assetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdFixedassetsChangesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsChangesGet(body);
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
| **assetId** | `string` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;FixedAssetChangeDto&gt;**](FixedAssetChangeDto.md)

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


## accountsetsAccountSetIdFixedassetsCheckGet

> Array&lt;FixedAssetCheckRowDto&gt; accountsetsAccountSetIdFixedassetsCheckGet(accountSetId)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsCheckGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdFixedassetsCheckGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsCheckGet(body);
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

[**Array&lt;FixedAssetCheckRowDto&gt;**](FixedAssetCheckRowDto.md)

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


## accountsetsAccountSetIdFixedassetsDepreciatePost

> DepreciatePeriodResultDto accountsetsAccountSetIdFixedassetsDepreciatePost(accountSetId, depreciatePeriodRequest)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsDepreciatePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // DepreciatePeriodRequest
    depreciatePeriodRequest: ...,
  } satisfies AccountsetsAccountSetIdFixedassetsDepreciatePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsDepreciatePost(body);
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
| **depreciatePeriodRequest** | [DepreciatePeriodRequest](DepreciatePeriodRequest.md) |  | |

### Return type

[**DepreciatePeriodResultDto**](DepreciatePeriodResultDto.md)

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


## accountsetsAccountSetIdFixedassetsDepreciationLedgerGet

> Array&lt;DepreciationLedgerRowDto&gt; accountsetsAccountSetIdFixedassetsDepreciationLedgerGet(accountSetId, period)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsDepreciationLedgerGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    period: 56,
  } satisfies AccountsetsAccountSetIdFixedassetsDepreciationLedgerGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsDepreciationLedgerGet(body);
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
| **period** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;DepreciationLedgerRowDto&gt;**](DepreciationLedgerRowDto.md)

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


## accountsetsAccountSetIdFixedassetsSummaryGet

> Array&lt;FixedAssetSummaryRowDto&gt; accountsetsAccountSetIdFixedassetsSummaryGet(accountSetId, startPeriod, endPeriod, byCategory)



### Example

```ts
import {
  Configuration,
  FixedAssetsApi,
} from '';
import type { AccountsetsAccountSetIdFixedassetsSummaryGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new FixedAssetsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    startPeriod: 56,
    // number (optional)
    endPeriod: 56,
    // boolean (optional)
    byCategory: true,
  } satisfies AccountsetsAccountSetIdFixedassetsSummaryGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdFixedassetsSummaryGet(body);
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
| **startPeriod** | `number` |  | [Optional] [Defaults to `undefined`] |
| **endPeriod** | `number` |  | [Optional] [Defaults to `undefined`] |
| **byCategory** | `boolean` |  | [Optional] [Defaults to `false`] |

### Return type

[**Array&lt;FixedAssetSummaryRowDto&gt;**](FixedAssetSummaryRowDto.md)

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

