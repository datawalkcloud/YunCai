# WeatherForecastApi

All URIs are relative to *http://localhost:5271*

| Method | HTTP request | Description |
|------------- | ------------- | -------------|
| [**getWeatherForecast**](WeatherForecastApi.md#getweatherforecast) | **GET** /WeatherForecast |  |



## getWeatherForecast

> Array&lt;WeatherForecast&gt; getWeatherForecast()



### Example

```ts
import {
  Configuration,
  WeatherForecastApi,
} from '';
import type { GetWeatherForecastRequest } from '';

async function example() {
  console.log("🚀 Testing  SDK...");
  const api = new WeatherForecastApi();

  try {
    const data = await api.getWeatherForecast();
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

[**Array&lt;WeatherForecast&gt;**](WeatherForecast.md)

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

