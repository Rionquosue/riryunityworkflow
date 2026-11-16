# Menu Mockup

## 1. Brief description

Lays out an example editor window from blocks.

## 2. What the tool can be used for

- Sketching how a tool window should look before it is built.

## 3. How the tool behaves

- Every component is a block; blocks nest and lay out like flexbox.
- Blocks are added from a Components palette. A **Tabs** block holds its own blocks per tab, styled like the Main Menu's tabs.
- Mockups are saved as `.yml` files in `Documents/RiryTools/Mockups`; nothing is compiled.

## 4. Flow chart

```mermaid
flowchart TD
    A[New or open mockup] --> B[Add blocks from the Components palette]
    B --> C[Nest and arrange blocks]
    C --> D[Preview the layout]
    D --> E[Save as .yml in Documents/RiryTools/Mockups]
```

## 5. References

- Flexbox layout rules.
