# BillsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdBillsGet**](BillsApi.md#accountsetsaccountsetidbillsget) | **GET** /accountsets/{accountSetId}/bills |  |
| [**accountsetsAccountSetIdBillsIdDelete**](BillsApi.md#accountsetsaccountsetidbillsiddelete) | **DELETE** /accountsets/{accountSetId}/bills/{id} |  |
| [**accountsetsAccountSetIdBillsIdPut**](BillsApi.md#accountsetsaccountsetidbillsidput) | **PUT** /accountsets/{accountSetId}/bills/{id} |  |
| [**accountsetsAccountSetIdBillsIdStatusPost**](BillsApi.md#accountsetsaccountsetidbillsidstatuspost) | **POST** /accountsets/{accountSetId}/bills/{id}/status |  |
| [**accountsetsAccountSetIdBillsPost**](BillsApi.md#accountsetsaccountsetidbillspost) | **POST** /accountsets/{accountSetId}/bills |  |



## accountsetsAccountSetIdBillsGet

> Array&lt;BillDto&gt; accountsetsAccountSetIdBillsGet(accountSetId, startDate, endDate)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
  } satisfies AccountsetsAccountSetIdBillsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsGet(body);
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
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;BillDto&gt;**](BillDto.md)

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


## accountsetsAccountSetIdBillsIdDelete

> accountsetsAccountSetIdBillsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdBillsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdDelete(body);
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


## accountsetsAccountSetIdBillsIdPut

> BillDto accountsetsAccountSetIdBillsIdPut(accountSetId, id, saveBillRequest)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveBillRequest
    saveBillRequest: ...,
  } satisfies AccountsetsAccountSetIdBillsIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdPut(body);
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
| **saveBillRequest** | [SaveBillRequest](SaveBillRequest.md) |  | |

### Return type

[**BillDto**](BillDto.md)

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


## accountsetsAccountSetIdBillsIdStatusPost

> BillDto accountsetsAccountSetIdBillsIdStatusPost(accountSetId, id, changeBillStatusRequest)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsIdStatusPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ChangeBillStatusRequest
    changeBillStatusRequest: ...,
  } satisfies AccountsetsAccountSetIdBillsIdStatusPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsIdStatusPost(body);
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
| **changeBillStatusRequest** | [ChangeBillStatusRequest](ChangeBillStatusRequest.md) |  | |

### Return type

[**BillDto**](BillDto.md)

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


## accountsetsAccountSetIdBillsPost

> BillDto accountsetsAccountSetIdBillsPost(accountSetId, saveBillRequest)



### Example

```ts
import {
  Configuration,
  BillsApi,
} from '';
import type { AccountsetsAccountSetIdBillsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new BillsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveBillRequest
    saveBillRequest: ...,
  } satisfies AccountsetsAccountSetIdBillsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdBillsPost(body);
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
| **saveBillRequest** | [SaveBillRequest](SaveBillRequest.md) |  | |

### Return type

[**BillDto**](BillDto.md)

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

