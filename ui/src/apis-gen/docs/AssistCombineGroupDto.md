
# AssistCombineGroupDto


## Properties

Name | Type
------------ | -------------
`code` | string
`name` | string
`cells` | [Array&lt;AssistCombineCellDto&gt;](AssistCombineCellDto.md)
`totalDebit` | number
`totalCredit` | number

## Example

```typescript
import type { AssistCombineGroupDto } from ''

// TODO: Update the object below with actual values
const example = {
  "code": null,
  "name": null,
  "cells": null,
  "totalDebit": null,
  "totalCredit": null,
} satisfies AssistCombineGroupDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AssistCombineGroupDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


