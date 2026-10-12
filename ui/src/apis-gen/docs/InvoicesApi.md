# InvoicesApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdInvoicesFundOverviewGet**](InvoicesApi.md#accountsetsaccountsetidinvoicesfundoverviewget) | **GET** /accountsets/{accountSetId}/invoices/fund-overview |  |
| [**accountsetsAccountSetIdInvoicesGet**](InvoicesApi.md#accountsetsaccountsetidinvoicesget) | **GET** /accountsets/{accountSetId}/invoices |  |
| [**accountsetsAccountSetIdInvoicesIdAuthenticatePost**](InvoicesApi.md#accountsetsaccountsetidinvoicesidauthenticatepost) | **POST** /accountsets/{accountSetId}/invoices/{id}/authenticate |  |
| [**accountsetsAccountSetIdInvoicesIdDelete**](InvoicesApi.md#accountsetsaccountsetidinvoicesiddelete) | **DELETE** /accountsets/{accountSetId}/invoices/{id} |  |
| [**accountsetsAccountSetIdInvoicesIdPut**](InvoicesApi.md#accountsetsaccountsetidinvoicesidput) | **PUT** /accountsets/{accountSetId}/invoices/{id} |  |
| [**accountsetsAccountSetIdInvoicesIdStatusPost**](InvoicesApi.md#accountsetsaccountsetidinvoicesidstatuspost) | **POST** /accountsets/{accountSetId}/invoices/{id}/status |  |
| [**accountsetsAccountSetIdInvoicesPost**](InvoicesApi.md#accountsetsaccountsetidinvoicespost) | **POST** /accountsets/{accountSetId}/invoices |  |
| [**accountsetsAccountSetIdInvoicesSummaryGet**](InvoicesApi.md#accountsetsaccountsetidinvoicessummaryget) | **GET** /accountsets/{accountSetId}/invoices/summary |  |



## accountsetsAccountSetIdInvoicesFundOverviewGet

> Array&lt;InvoiceFundOverviewRowDto&gt; accountsetsAccountSetIdInvoicesFundOverviewGet(accountSetId, startDate, endDate)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesFundOverviewGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
  } satisfies AccountsetsAccountSetIdInvoicesFundOverviewGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesFundOverviewGet(body);
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

[**Array&lt;InvoiceFundOverviewRowDto&gt;**](InvoiceFundOverviewRowDto.md)

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


## accountsetsAccountSetIdInvoicesGet

> Array&lt;InvoiceDto&gt; accountsetsAccountSetIdInvoicesGet(accountSetId, direction, startDate, endDate, status)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    direction: 56,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // number (optional)
    status: 56,
  } satisfies AccountsetsAccountSetIdInvoicesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesGet(body);
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
| **direction** | `number` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **status** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**Array&lt;InvoiceDto&gt;**](InvoiceDto.md)

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


## accountsetsAccountSetIdInvoicesIdAuthenticatePost

> InvoiceDto accountsetsAccountSetIdInvoicesIdAuthenticatePost(accountSetId, id, authenticateInvoiceRequest)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesIdAuthenticatePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // AuthenticateInvoiceRequest
    authenticateInvoiceRequest: ...,
  } satisfies AccountsetsAccountSetIdInvoicesIdAuthenticatePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesIdAuthenticatePost(body);
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
| **authenticateInvoiceRequest** | [AuthenticateInvoiceRequest](AuthenticateInvoiceRequest.md) |  | |

### Return type

[**InvoiceDto**](InvoiceDto.md)

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


## accountsetsAccountSetIdInvoicesIdDelete

> accountsetsAccountSetIdInvoicesIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdInvoicesIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesIdDelete(body);
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


## accountsetsAccountSetIdInvoicesIdPut

> InvoiceDto accountsetsAccountSetIdInvoicesIdPut(accountSetId, id, saveInvoiceRequest)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveInvoiceRequest
    saveInvoiceRequest: ...,
  } satisfies AccountsetsAccountSetIdInvoicesIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesIdPut(body);
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
| **saveInvoiceRequest** | [SaveInvoiceRequest](SaveInvoiceRequest.md) |  | |

### Return type

[**InvoiceDto**](InvoiceDto.md)

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


## accountsetsAccountSetIdInvoicesIdStatusPost

> InvoiceDto accountsetsAccountSetIdInvoicesIdStatusPost(accountSetId, id, changeInvoiceStatusRequest)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesIdStatusPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ChangeInvoiceStatusRequest
    changeInvoiceStatusRequest: ...,
  } satisfies AccountsetsAccountSetIdInvoicesIdStatusPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesIdStatusPost(body);
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
| **changeInvoiceStatusRequest** | [ChangeInvoiceStatusRequest](ChangeInvoiceStatusRequest.md) |  | |

### Return type

[**InvoiceDto**](InvoiceDto.md)

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


## accountsetsAccountSetIdInvoicesPost

> InvoiceDto accountsetsAccountSetIdInvoicesPost(accountSetId, saveInvoiceRequest, direction)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveInvoiceRequest
    saveInvoiceRequest: ...,
    // number (optional)
    direction: 56,
  } satisfies AccountsetsAccountSetIdInvoicesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesPost(body);
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
| **saveInvoiceRequest** | [SaveInvoiceRequest](SaveInvoiceRequest.md) |  | |
| **direction** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**InvoiceDto**](InvoiceDto.md)

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


## accountsetsAccountSetIdInvoicesSummaryGet

> InvoiceSummaryDto accountsetsAccountSetIdInvoicesSummaryGet(accountSetId, direction, startDate, endDate, status)



### Example

```ts
import {
  Configuration,
  InvoicesApi,
} from '';
import type { AccountsetsAccountSetIdInvoicesSummaryGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new InvoicesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    direction: 56,
    // Date (optional)
    startDate: 2013-10-20,
    // Date (optional)
    endDate: 2013-10-20,
    // number (optional)
    status: 56,
  } satisfies AccountsetsAccountSetIdInvoicesSummaryGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdInvoicesSummaryGet(body);
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
| **direction** | `number` |  | [Optional] [Defaults to `undefined`] |
| **startDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **endDate** | `Date` |  | [Optional] [Defaults to `undefined`] |
| **status** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**InvoiceSummaryDto**](InvoiceSummaryDto.md)

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

