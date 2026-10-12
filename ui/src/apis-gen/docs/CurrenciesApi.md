# CurrenciesApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdCurrenciesGet**](CurrenciesApi.md#accountsetsaccountsetidcurrenciesget) | **GET** /accountsets/{accountSetId}/currencies |  |
| [**accountsetsAccountSetIdCurrenciesIdDelete**](CurrenciesApi.md#accountsetsaccountsetidcurrenciesiddelete) | **DELETE** /accountsets/{accountSetId}/currencies/{id} |  |
| [**accountsetsAccountSetIdCurrenciesIdPut**](CurrenciesApi.md#accountsetsaccountsetidcurrenciesidput) | **PUT** /accountsets/{accountSetId}/currencies/{id} |  |
| [**accountsetsAccountSetIdCurrenciesPost**](CurrenciesApi.md#accountsetsaccountsetidcurrenciespost) | **POST** /accountsets/{accountSetId}/currencies |  |



## accountsetsAccountSetIdCurrenciesGet

> Array&lt;CurrencyDto&gt; accountsetsAccountSetIdCurrenciesGet(accountSetId)



### Example

```ts
import {
  Configuration,
  CurrenciesApi,
} from '';
import type { AccountsetsAccountSetIdCurrenciesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CurrenciesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdCurrenciesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdCurrenciesGet(body);
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

[**Array&lt;CurrencyDto&gt;**](CurrencyDto.md)

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


## accountsetsAccountSetIdCurrenciesIdDelete

> accountsetsAccountSetIdCurrenciesIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  CurrenciesApi,
} from '';
import type { AccountsetsAccountSetIdCurrenciesIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CurrenciesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdCurrenciesIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdCurrenciesIdDelete(body);
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


## accountsetsAccountSetIdCurrenciesIdPut

> CurrencyDto accountsetsAccountSetIdCurrenciesIdPut(accountSetId, id, saveCurrencyRequest)



### Example

```ts
import {
  Configuration,
  CurrenciesApi,
} from '';
import type { AccountsetsAccountSetIdCurrenciesIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CurrenciesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveCurrencyRequest
    saveCurrencyRequest: ...,
  } satisfies AccountsetsAccountSetIdCurrenciesIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdCurrenciesIdPut(body);
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
| **saveCurrencyRequest** | [SaveCurrencyRequest](SaveCurrencyRequest.md) |  | |

### Return type

[**CurrencyDto**](CurrencyDto.md)

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


## accountsetsAccountSetIdCurrenciesPost

> CurrencyDto accountsetsAccountSetIdCurrenciesPost(accountSetId, saveCurrencyRequest)



### Example

```ts
import {
  Configuration,
  CurrenciesApi,
} from '';
import type { AccountsetsAccountSetIdCurrenciesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new CurrenciesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveCurrencyRequest
    saveCurrencyRequest: ...,
  } satisfies AccountsetsAccountSetIdCurrenciesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdCurrenciesPost(body);
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
| **saveCurrencyRequest** | [SaveCurrencyRequest](SaveCurrencyRequest.md) |  | |

### Return type

[**CurrencyDto**](CurrencyDto.md)

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

