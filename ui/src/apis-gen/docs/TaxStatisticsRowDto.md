
# TaxStatisticsRowDto


## Properties

Name | Type
------------ | -------------
`code` | string
`name` | string
`line` | number
`currentAmount` | number
`currentPaidAmount` | number
`currentYoyPercent` | number
`yearToDateAmount` | number
`yearToDatePaidAmount` | number
`yearToDateYoyPercent` | number

## Example

```typescript
import type { TaxStatisticsRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "code": null,
  "name": null,
  "line": null,
  "currentAmount": null,
  "currentPaidAmount": null,
  "currentYoyPercent": null,
  "yearToDateAmount": null,
  "yearToDatePaidAmount": null,
  "yearToDateYoyPercent": null,
} satisfies TaxStatisticsRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxStatisticsRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


