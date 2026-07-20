
# AccountSetDto


## Properties

Name | Type
------------ | -------------
`id` | string
`name` | string
`companyName` | string
`standard` | number
`taxpayerType` | number
`baseCurrency` | string
`startYear` | number
`startMonth` | number
`startPeriod` | string
`isCurrent` | boolean

## Example

```typescript
import type { AccountSetDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "name": null,
  "companyName": null,
  "standard": null,
  "taxpayerType": null,
  "baseCurrency": null,
  "startYear": null,
  "startMonth": null,
  "startPeriod": null,
  "isCurrent": null,
} satisfies AccountSetDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AccountSetDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


