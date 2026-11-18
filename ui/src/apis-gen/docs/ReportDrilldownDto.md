
# ReportDrilldownDto


## Properties

Name | Type
------------ | -------------
`line` | number
`year` | number
`month` | number
`periodText` | string
`accounts` | [Array&lt;ReportDrilldownAccountDto&gt;](ReportDrilldownAccountDto.md)
`totalCurrent` | number
`totalYearToDate` | number

## Example

```typescript
import type { ReportDrilldownDto } from ''

// TODO: Update the object below with actual values
const example = {
  "line": null,
  "year": null,
  "month": null,
  "periodText": null,
  "accounts": null,
  "totalCurrent": null,
  "totalYearToDate": null,
} satisfies ReportDrilldownDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ReportDrilldownDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


