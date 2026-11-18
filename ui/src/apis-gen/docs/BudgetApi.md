# BudgetApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdBudgetsGet**](BudgetApi.md#accountsetsaccountsetidbudgetsget) | **GET** /accountsets/{accountSetId}/budgets |  |
| [**accountsetsAccountSetIdBudgetsIdDelete**](BudgetApi.md#accountsetsaccountsetidbudgetsiddelete) | **DELETE** /accountsets/{accountSetId}/budgets/{id} |  |
| [**accountsetsAccountSetIdBudgetsIdExecutionGet**](BudgetApi.md#accountsetsaccountsetidbudgetsidexecutionget) | **GET** /accountsets/{accountSetId}/budgets/{id}/execution |  |
| [**accountsetsAccountSetIdBudgetsIdGet**](BudgetApi.md#accountsetsaccountsetidbudgetsidget) | **GET** /accountsets/{accountSetId}/budgets/{id} |  |
| [**accountsetsAccountSetIdBudgetsIdPut**](BudgetApi.md#accountsetsaccountsetidbudgetsidput) | **PUT** /accountsets/{accountSetId}/budgets/{id} |  |
| [**accountsetsAccountSetIdBudgetsPost**](BudgetApi.md#accountsetsaccountsetidbudgetspost) | **POST** /accountsets/{accountSetId}/budgets |  |



## accountsetsAccountSetIdBudgetsGet

> Array&lt;BudgetSummaryDto&gt; accountsetsAccountSetIdBudgetsGet(accountSetId, year)



### Example

```ts
import {
  Configuration,
  BudgetApi,
} from '';
import type { AccountsetsAccountSetIdBudgetsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BudgetApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
  } satisfies AccountsetsAccountSetIdBudgetsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdBudgetsGet(body);
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
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;BudgetSummaryDto&gt;**](BudgetSummaryDto.md)

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


## accountsetsAccountSetIdBudgetsIdDelete

> accountsetsAccountSetIdBudgetsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  BudgetApi,
} from '';
import type { AccountsetsAccountSetIdBudgetsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BudgetApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdBudgetsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdBudgetsIdDelete(body);
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


## accountsetsAccountSetIdBudgetsIdExecutionGet

> BudgetExecutionDto accountsetsAccountSetIdBudgetsIdExecutionGet(accountSetId, id, throughMonth)



### Example

```ts
import {
  Configuration,
  BudgetApi,
} from '';
import type { AccountsetsAccountSetIdBudgetsIdExecutionGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BudgetApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    throughMonth: 56,
  } satisfies AccountsetsAccountSetIdBudgetsIdExecutionGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdBudgetsIdExecutionGet(body);
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
| **throughMonth** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**BudgetExecutionDto**](BudgetExecutionDto.md)

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


## accountsetsAccountSetIdBudgetsIdGet

> BudgetDto accountsetsAccountSetIdBudgetsIdGet(accountSetId, id)



### Example

```ts
import {
  Configuration,
  BudgetApi,
} from '';
import type { AccountsetsAccountSetIdBudgetsIdGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BudgetApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdBudgetsIdGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdBudgetsIdGet(body);
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

[**BudgetDto**](BudgetDto.md)

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


## accountsetsAccountSetIdBudgetsIdPut

> BudgetDto accountsetsAccountSetIdBudgetsIdPut(accountSetId, id, saveBudgetRequest)



### Example

```ts
import {
  Configuration,
  BudgetApi,
} from '';
import type { AccountsetsAccountSetIdBudgetsIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BudgetApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveBudgetRequest
    saveBudgetRequest: ...,
  } satisfies AccountsetsAccountSetIdBudgetsIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdBudgetsIdPut(body);
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
| **saveBudgetRequest** | [SaveBudgetRequest](SaveBudgetRequest.md) |  | |

### Return type

[**BudgetDto**](BudgetDto.md)

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


## accountsetsAccountSetIdBudgetsPost

> BudgetDto accountsetsAccountSetIdBudgetsPost(accountSetId, saveBudgetRequest)



### Example

```ts
import {
  Configuration,
  BudgetApi,
} from '';
import type { AccountsetsAccountSetIdBudgetsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BudgetApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveBudgetRequest
    saveBudgetRequest: ...,
  } satisfies AccountsetsAccountSetIdBudgetsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBudgetsPost(body);
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
| **saveBudgetRequest** | [SaveBudgetRequest](SaveBudgetRequest.md) |  | |

### Return type

[**BudgetDto**](BudgetDto.md)

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

