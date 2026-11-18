import { useCallback, useRef, useState } from "react"

/** 表单错误集合：键为字段名，值为提示文案。 */
export type FormErrors<T> = Partial<Record<keyof T, string>>

export interface LocalForm<T> {
    values: T
    errors: FormErrors<T>
    /** 打开弹窗 / 抽屉时整体预填，对应原 setFieldsValue。 */
    fill: (next: T) => void
    /** 改单个字段，对应原控件的 onChange。 */
    set: <K extends keyof T>(key: K, value: T[K]) => void
    /** 一次改多个字段（如切换类型时同时清空附属字段）。 */
    patch: (partial: Partial<T>) => void
    /** 提交前校验，返回是否通过，对应原 validateFields。 */
    check: () => boolean
}

/**
 * 极简受控表单，替代原 antd Form 实例：值 + 错误 + 提交前校验。
 * 校验只在提交时执行；一旦出现错误，后续改动即重算，等价原 dependencies 联动。
 */
export function useLocalForm<T extends object>(
    initial: T,
    validate: (values: T) => FormErrors<T>,
): LocalForm<T> {
    const [state, setState] = useState<{ values: T; errors: FormErrors<T> }>(() => ({
        values: initial,
        errors: {},
    }))
    // validate 由各页内联传入，每次渲染都是新函数；用 ref 持有最新值，不参与依赖数组。
    const validateRef = useRef(validate)
    validateRef.current = validate
    const valuesRef = useRef(state.values)
    valuesRef.current = state.values

    const patch = useCallback((partial: Partial<T>) => {
        setState((cur) => {
            const values = { ...cur.values, ...partial }
            return {
                values,
                errors: Object.keys(cur.errors).length > 0 ? validateRef.current(values) : cur.errors,
            }
        })
    }, [])

    const set = useCallback(
        <K extends keyof T>(key: K, value: T[K]) => {
            const partial: Partial<T> = {}
            partial[key] = value
            patch(partial)
        },
        [patch],
    )

    const fill = useCallback((next: T) => setState({ values: next, errors: {} }), [])

    const check = useCallback(() => {
        const errors = validateRef.current(valuesRef.current)
        setState((cur) => ({ ...cur, errors }))
        return Object.keys(errors).length === 0
    }, [])

    return { values: state.values, errors: state.errors, fill, set, patch, check }
}
