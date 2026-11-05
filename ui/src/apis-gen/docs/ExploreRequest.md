
# ExploreRequest


## Properties

Name | Type
------------ | -------------
`kind` | number
`path` | Array&lt;string&gt;
`fill` | { [key: string]: string; }
`click` | Array&lt;string&gt;
`evaluate` | string

## Example

```typescript
import type { ExploreRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "kind": null,
  "path": null,
  "fill": null,
  "click": null,
  "evaluate": null,
} satisfies ExploreRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ExploreRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


