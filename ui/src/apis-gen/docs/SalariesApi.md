# SalariesApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**accountsetsAccountSetIdSalaryAccrueViewGet**](SalariesApi.md#accountsetsaccountsetidsalaryaccrueviewget) | **GET** /accountsets/{accountSetId}/salary/accrue-view |  |
| [**accountsetsAccountSetIdSalaryBillsGet**](SalariesApi.md#accountsetsaccountsetidsalarybillsget) | **GET** /accountsets/{accountSetId}/salary/bills |  |
| [**accountsetsAccountSetIdSalaryBillsIdDelete**](SalariesApi.md#accountsetsaccountsetidsalarybillsiddelete) | **DELETE** /accountsets/{accountSetId}/salary/bills/{id} |  |
| [**accountsetsAccountSetIdSalaryBillsPost**](SalariesApi.md#accountsetsaccountsetidsalarybillspost) | **POST** /accountsets/{accountSetId}/salary/bills |  |
| [**accountsetsAccountSetIdSalaryEmployeesGet**](SalariesApi.md#accountsetsaccountsetidsalaryemployeesget) | **GET** /accountsets/{accountSetId}/salary/employees |  |
| [**accountsetsAccountSetIdSalaryEmployeesIdDelete**](SalariesApi.md#accountsetsaccountsetidsalaryemployeesiddelete) | **DELETE** /accountsets/{accountSetId}/salary/employees/{id} |  |
| [**accountsetsAccountSetIdSalaryEmployeesIdPut**](SalariesApi.md#accountsetsaccountsetidsalaryemployeesidput) | **PUT** /accountsets/{accountSetId}/salary/employees/{id} |  |
| [**accountsetsAccountSetIdSalaryEmployeesPost**](SalariesApi.md#accountsetsaccountsetidsalaryemployeespost) | **POST** /accountsets/{accountSetId}/salary/employees |  |
| [**accountsetsAccountSetIdSalaryGenerateAccruePost**](SalariesApi.md#accountsetsaccountsetidsalarygenerateaccruepost) | **POST** /accountsets/{accountSetId}/salary/generate-accrue |  |
| [**accountsetsAccountSetIdSalaryGeneratePayPost**](SalariesApi.md#accountsetsaccountsetidsalarygeneratepaypost) | **POST** /accountsets/{accountSetId}/salary/generate-pay |  |
| [**accountsetsAccountSetIdSalaryInsuranceGet**](SalariesApi.md#accountsetsaccountsetidsalaryinsuranceget) | **GET** /accountsets/{accountSetId}/salary/insurance |  |
| [**accountsetsAccountSetIdSalaryInsuranceIdDelete**](SalariesApi.md#accountsetsaccountsetidsalaryinsuranceiddelete) | **DELETE** /accountsets/{accountSetId}/salary/insurance/{id} |  |
| [**accountsetsAccountSetIdSalaryInsuranceIdPut**](SalariesApi.md#accountsetsaccountsetidsalaryinsuranceidput) | **PUT** /accountsets/{accountSetId}/salary/insurance/{id} |  |
| [**accountsetsAccountSetIdSalaryInsurancePost**](SalariesApi.md#accountsetsaccountsetidsalaryinsurancepost) | **POST** /accountsets/{accountSetId}/salary/insurance |  |
| [**accountsetsAccountSetIdSalarySummaryGet**](SalariesApi.md#accountsetsaccountsetidsalarysummaryget) | **GET** /accountsets/{accountSetId}/salary/summary |  |



## accountsetsAccountSetIdSalaryAccrueViewGet

> AccrueViewDto accountsetsAccountSetIdSalaryAccrueViewGet(accountSetId, period)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryAccrueViewGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    period: 56,
  } satisfies AccountsetsAccountSetIdSalaryAccrueViewGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryAccrueViewGet(body);
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

[**AccrueViewDto**](AccrueViewDto.md)

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


## accountsetsAccountSetIdSalaryBillsGet

> Array&lt;SalaryBillDto&gt; accountsetsAccountSetIdSalaryBillsGet(accountSetId, period)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryBillsGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    period: 56,
  } satisfies AccountsetsAccountSetIdSalaryBillsGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryBillsGet(body);
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

[**Array&lt;SalaryBillDto&gt;**](SalaryBillDto.md)

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


## accountsetsAccountSetIdSalaryBillsIdDelete

> accountsetsAccountSetIdSalaryBillsIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryBillsIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryBillsIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryBillsIdDelete(body);
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


## accountsetsAccountSetIdSalaryBillsPost

> SalaryBillDto accountsetsAccountSetIdSalaryBillsPost(accountSetId, saveSalaryBillRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryBillsPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveSalaryBillRequest
    saveSalaryBillRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryBillsPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryBillsPost(body);
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
| **saveSalaryBillRequest** | [SaveSalaryBillRequest](SaveSalaryBillRequest.md) |  | |

### Return type

[**SalaryBillDto**](SalaryBillDto.md)

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


## accountsetsAccountSetIdSalaryEmployeesGet

> Array&lt;EmployeeDto&gt; accountsetsAccountSetIdSalaryEmployeesGet(accountSetId)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesGet(body);
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

[**Array&lt;EmployeeDto&gt;**](EmployeeDto.md)

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


## accountsetsAccountSetIdSalaryEmployeesIdDelete

> accountsetsAccountSetIdSalaryEmployeesIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesIdDelete(body);
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


## accountsetsAccountSetIdSalaryEmployeesIdPut

> EmployeeDto accountsetsAccountSetIdSalaryEmployeesIdPut(accountSetId, id, saveEmployeeRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveEmployeeRequest
    saveEmployeeRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesIdPut(body);
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
| **saveEmployeeRequest** | [SaveEmployeeRequest](SaveEmployeeRequest.md) |  | |

### Return type

[**EmployeeDto**](EmployeeDto.md)

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


## accountsetsAccountSetIdSalaryEmployeesPost

> EmployeeDto accountsetsAccountSetIdSalaryEmployeesPost(accountSetId, saveEmployeeRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryEmployeesPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveEmployeeRequest
    saveEmployeeRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryEmployeesPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryEmployeesPost(body);
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
| **saveEmployeeRequest** | [SaveEmployeeRequest](SaveEmployeeRequest.md) |  | |

### Return type

[**EmployeeDto**](EmployeeDto.md)

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


## accountsetsAccountSetIdSalaryGenerateAccruePost

> GenerateCountDto accountsetsAccountSetIdSalaryGenerateAccruePost(accountSetId, generateSalaryVoucherRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryGenerateAccruePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GenerateSalaryVoucherRequest
    generateSalaryVoucherRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryGenerateAccruePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryGenerateAccruePost(body);
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
| **generateSalaryVoucherRequest** | [GenerateSalaryVoucherRequest](GenerateSalaryVoucherRequest.md) |  | |

### Return type

[**GenerateCountDto**](GenerateCountDto.md)

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


## accountsetsAccountSetIdSalaryGeneratePayPost

> GenerateCountDto accountsetsAccountSetIdSalaryGeneratePayPost(accountSetId, generateSalaryVoucherRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryGeneratePayPostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // GenerateSalaryVoucherRequest
    generateSalaryVoucherRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryGeneratePayPostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryGeneratePayPost(body);
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
| **generateSalaryVoucherRequest** | [GenerateSalaryVoucherRequest](GenerateSalaryVoucherRequest.md) |  | |

### Return type

[**GenerateCountDto**](GenerateCountDto.md)

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


## accountsetsAccountSetIdSalaryInsuranceGet

> Array&lt;InsuranceItemDto&gt; accountsetsAccountSetIdSalaryInsuranceGet(accountSetId)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryInsuranceGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryInsuranceGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryInsuranceGet(body);
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

[**Array&lt;InsuranceItemDto&gt;**](InsuranceItemDto.md)

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


## accountsetsAccountSetIdSalaryInsuranceIdDelete

> accountsetsAccountSetIdSalaryInsuranceIdDelete(accountSetId, id)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryInsuranceIdDeleteRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
  } satisfies AccountsetsAccountSetIdSalaryInsuranceIdDeleteRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryInsuranceIdDelete(body);
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


## accountsetsAccountSetIdSalaryInsuranceIdPut

> InsuranceItemDto accountsetsAccountSetIdSalaryInsuranceIdPut(accountSetId, id, saveInsuranceRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryInsuranceIdPutRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // string
    id: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveInsuranceRequest
    saveInsuranceRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryInsuranceIdPutRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryInsuranceIdPut(body);
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
| **saveInsuranceRequest** | [SaveInsuranceRequest](SaveInsuranceRequest.md) |  | |

### Return type

[**InsuranceItemDto**](InsuranceItemDto.md)

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


## accountsetsAccountSetIdSalaryInsurancePost

> InsuranceItemDto accountsetsAccountSetIdSalaryInsurancePost(accountSetId, saveInsuranceRequest)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalaryInsurancePostRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // SaveInsuranceRequest
    saveInsuranceRequest: ...,
  } satisfies AccountsetsAccountSetIdSalaryInsurancePostRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalaryInsurancePost(body);
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
| **saveInsuranceRequest** | [SaveInsuranceRequest](SaveInsuranceRequest.md) |  | |

### Return type

[**InsuranceItemDto**](InsuranceItemDto.md)

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


## accountsetsAccountSetIdSalarySummaryGet

> Array&lt;SalarySummaryRowDto&gt; accountsetsAccountSetIdSalarySummaryGet(accountSetId, startPeriod, endPeriod)



### Example

```ts
import {
  Configuration,
  SalariesApi,
} from '';
import type { AccountsetsAccountSetIdSalarySummaryGetRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new SalariesApi();

  const body = {
    // string
    accountSetId: 38400000-8cf0-11bd-b23e-10b96e4ef00d,
    // number (optional)
    startPeriod: 56,
    // number (optional)
    endPeriod: 56,
  } satisfies AccountsetsAccountSetIdSalarySummaryGetRequest;

  try {
    const data = await api.accountsetsAccountSetIdSalarySummaryGet(body);
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

### Return type

[**Array&lt;SalarySummaryRowDto&gt;**](SalarySummaryRowDto.md)

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

