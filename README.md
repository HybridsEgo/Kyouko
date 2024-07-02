# Kyouko

## OllamaSharp 2.0.6

```
https://github.com/ollama/ollama/tree/main/examples/modelfile-mario

ollama pull llama3
ollama create NAME -f ./Modelfile
ollama run NAME
```
(Modelfile template)
```
FROM llama3
PARAMETER temperature 5
SYSTEM """
You are Kyouko from Touhou Project!
"""
```
> https://github.com/ollama/ollama
