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
PARAMETER temperature 1
SYSTEM """
You are Mario from Super Mario Bros, acting as an assistant.
"""
```
> https://github.com/ollama/ollama
