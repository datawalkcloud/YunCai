# AccountSetsApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountSetsGet**](AccountSetsApi.md#accountsetsget) | **GET** /AccountSets |  |
| [**accountSetsIdClosePeriodPost**](AccountSetsApi.md#accountsetsidcloseperiodpost) | **POST** /AccountSets/{id}/close-period |  |
| [**accountSetsIdCurrentPost**](AccountSetsApi.md#accountsetsidcurrentpost) | **POST** /AccountSets/{id}/current |  |
| [**accountSetsIdDelete**](AccountSetsApi.md#accountsetsiddelete) | **DELETE** /AccountSets/{id} |  |
| [**accountSetsIdGet**](AccountSetsApi.md#accountsetsidget) | **GET** /AccountSets/{id} |  |
| [**accountSetsIdPut**](AccountSetsApi.md#accountsetsidput) | **PUT** /AccountSets/{id} |  |
| [**accountSetsIdReopenPeriodPost**](AccountSetsApi.md#accountsetsidreopenperiodpost) | **POST** /AccountSets/{id}/reopen-period |  |
| [**accountSetsPost**](AccountSetsApi.md#accountsetspost) | **POST** /AccountSets |  |



## accountSetsGet

> Array&lt;AccountSetDto&gt; accountSetsGet()



### Example

```ts
import {
  Configuration,
  AccountSetsApi,
} from '';
import type { AccountSetsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountSetsApi();

  try {
    const data = await api.accountSetsGet();
    console.log(data);
  } catch (error) {
    console.error(error);
  }
}

// Run the test
example().catch(console.error);
```

### Parameters

This endpoint does not need any parameter.

### Return type

[**Array&lt;AccountSetDto&gt;**](AccountSetDto.md)

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


## accountSetsIdClosePeriodPost

> AccountSetDto accountSetsIdClosePeriodPost(id, closePeriodRequest)



### Example

```ts
import {
  Configuration,
  AccountSetsApi,
} from '';
import type { AccountSetsIdClosePeriodPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountSetsApi();

  const body = {
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // ClosePeriodRequest
    closePeriodRequest: ...,
  } satisfies AccountSetsIdClosePeriodPostRequest;

  try {
    const data = await api.accountSetsIdClosePeriodPost(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |
| **closePeriodRequest** | [ClosePeriodRequest](ClosePeriodRequest.md) |  | |

### Return type

[**AccountSetDto**](AccountSetDto.md)

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


## accountSetsIdCurrentPost

> AccountSetDto accountSetsIdCurrentPost(id)



### Example

```ts
import {
  Configuration,
  AccountSetsApi,
} from '';
import type { AccountSetsIdCurrentPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountSetsApi();

  const body = {
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountSetsIdCurrentPostRequest;

  try {
    const data = await api.accountSetsIdCurrentPost(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |

### Return type

[**AccountSetDto**](AccountSetDto.md)

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


## accountSetsIdDelete

> accountSetsIdDelete(id)



### Example

```ts
import {
  Configuration,
  AccountSetsApi,
} from '';
import type { AccountSetsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountSetsApi();

  const body = {
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountSetsIdDeleteRequest;

  try {
    const data = await api.accountSetsIdDelete(body);
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


## accountSetsIdGet

> AccountSetDto accountSetsIdGet(id)



### Example

```ts
import {
  Configuration,
  AccountSetsApi,
} from '';
import type { AccountSetsIdGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountSetsApi();

  const body = {
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountSetsIdGetRequest;

  try {
    const data = await api.accountSetsIdGet(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |

### Return type

[**AccountSetDto**](AccountSetDto.md)

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


## accountSetsIdPut

> AccountSetDto accountSetsIdPut(id, updateAccountSetRequest)



### Example

```ts
import {
  Configuration,
  AccountSetsApi,
} from '';
import type { AccountSetsIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountSetsApi();

  const body = {
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // UpdateAccountSetRequest
    updateAccountSetRequest: ...,
  } satisfies AccountSetsIdPutRequest;

  try {
    const data = await api.accountSetsIdPut(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |
| **updateAccountSetRequest** | [UpdateAccountSetRequest](UpdateAccountSetRequest.md) |  | |

### Return type

[**AccountSetDto**](AccountSetDto.md)

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


## accountSetsIdReopenPeriodPost

> AccountSetDto accountSetsIdReopenPeriodPost(id)



### Example

```ts
import {
  Configuration,
  AccountSetsApi,
} from '';
import type { AccountSetsIdReopenPeriodPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountSetsApi();

  const body = {
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountSetsIdReopenPeriodPostRequest;

  try {
    const data = await api.accountSetsIdReopenPeriodPost(body);
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
| **id** | `string` |  | [Defaults to `undefined`] |

### Return type

[**AccountSetDto**](AccountSetDto.md)

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


## accountSetsPost

> AccountSetDto accountSetsPost(createAccountSetRequest)



### Example

```ts
import {
  Configuration,
  AccountSetsApi,
} from '';
import type { AccountSetsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new AccountSetsApi();

  const body = {
    // CreateAccountSetRequest
    createAccountSetRequest: ...,
  } satisfies AccountSetsPostRequest;

  try {
    const data = await api.accountSetsPost(body);
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
| **createAccountSetRequest** | [CreateAccountSetRequest](CreateAccountSetRequest.md) |  | |

### Return type

[**AccountSetDto**](AccountSetDto.md)

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

