
# WeatherForecast


## Properties

Name | Type
------------ | -------------
`date` | Date
`temperatureC` | [WeatherForecastTemperatureC](WeatherForecastTemperatureC.md)
`temperatureF` | [WeatherForecastTemperatureC](WeatherForecastTemperatureC.md)
`summary` | string

## Example

```typescript
import type { WeatherForecast } from ''

// TODO: Update the object below with actual values
const example = {
  "date": null,
  "temperatureC": null,
  "temperatureF": null,
  "summary": null,
} satisfies WeatherForecast

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as WeatherForecast
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


