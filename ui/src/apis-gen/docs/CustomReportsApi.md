# CustomReportsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdCustomReportsGet**](CustomReportsApi.md#accountsetsaccountsetidcustomreportsget) | **GET** /accountsets/{accountSetId}/custom-reports |  |
| [**accountsetsAccountSetIdCustomReportsIdDelete**](CustomReportsApi.md#accountsetsaccountsetidcustomreportsiddelete) | **DELETE** /accountsets/{accountSetId}/custom-reports/{id} |  |
| [**accountsetsAccountSetIdCustomReportsIdPut**](CustomReportsApi.md#accountsetsaccountsetidcustomreportsidput) | **PUT** /accountsets/{accountSetId}/custom-reports/{id} |  |
| [**accountsetsAccountSetIdCustomReportsIdRenderGet**](CustomReportsApi.md#accountsetsaccountsetidcustomreportsidrenderget) | **GET** /accountsets/{accountSetId}/custom-reports/{id}/render |  |
| [**accountsetsAccountSetIdCustomReportsPost**](CustomReportsApi.md#accountsetsaccountsetidcustomreportspost) | **POST** /accountsets/{accountSetId}/custom-reports |  |



## accountsetsAccountSetIdCustomReportsGet

> Array&lt;CustomReportDto&gt; accountsetsAccountSetIdCustomReportsGet(accountSetId)



### Example

```ts
import {
  Configuration,
  CustomReportsApi,
} from '';
import type { AccountsetsAccountSetIdCustomReportsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CustomReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdCustomReportsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdCustomReportsGet(body);
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

[**Array&lt;CustomReportDto&gt;**](CustomReportDto.md)

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


## accountsetsAccountSetIdCustomReportsIdDelete

> accountsetsAccountSetIdCustomReportsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  CustomReportsApi,
} from '';
import type { AccountsetsAccountSetIdCustomReportsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CustomReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdCustomReportsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdCustomReportsIdDelete(body);
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


## accountsetsAccountSetIdCustomReportsIdPut

> CustomReportDto accountsetsAccountSetIdCustomReportsIdPut(accountSetId, id, saveCustomReportRequest)



### Example

```ts
import {
  Configuration,
  CustomReportsApi,
} from '';
import type { AccountsetsAccountSetIdCustomReportsIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CustomReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveCustomReportRequest
    saveCustomReportRequest: ...,
  } satisfies AccountsetsAccountSetIdCustomReportsIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdCustomReportsIdPut(body);
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
| **saveCustomReportRequest** | [SaveCustomReportRequest](SaveCustomReportRequest.md) |  | |

### Return type

[**CustomReportDto**](CustomReportDto.md)

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


## accountsetsAccountSetIdCustomReportsIdRenderGet

> CustomReportRenderDto accountsetsAccountSetIdCustomReportsIdRenderGet(accountSetId, id, year)



### Example

```ts
import {
  Configuration,
  CustomReportsApi,
} from '';
import type { AccountsetsAccountSetIdCustomReportsIdRenderGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CustomReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    year: 56,
  } satisfies AccountsetsAccountSetIdCustomReportsIdRenderGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdCustomReportsIdRenderGet(body);
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
| **year** | `number` |  | [Optional] [Defaults to `undefined`] |

### Return type

[**CustomReportRenderDto**](CustomReportRenderDto.md)

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


## accountsetsAccountSetIdCustomReportsPost

> CustomReportDto accountsetsAccountSetIdCustomReportsPost(accountSetId, saveCustomReportRequest)



### Example

```ts
import {
  Configuration,
  CustomReportsApi,
} from '';
import type { AccountsetsAccountSetIdCustomReportsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CustomReportsApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveCustomReportRequest
    saveCustomReportRequest: ...,
  } satisfies AccountsetsAccountSetIdCustomReportsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdCustomReportsPost(body);
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
| **saveCustomReportRequest** | [SaveCustomReportRequest](SaveCustomReportRequest.md) |  | |

### Return type

[**CustomReportDto**](CustomReportDto.md)

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

