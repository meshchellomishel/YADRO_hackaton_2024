
Пункты плана, которые можно разработать, не дорабатывая функциональную модель СнК и DPI интерфейс

1. Проверить режим доступа к регистрам UART - read, write, read-write
2. Проверить начальные значений регистров (reset value) блока UART
3. 



Пункты плана, которые требуют не доработки функциональной модели СнК и DPI интерфейса

3. Разработка функциональных тестов UART для проверки корректной работы регистров, описанных в документации.
4. Проверка работы приема и передачи данных с использованием UART в двух режимах: по опросу (pooling mode), по прерыванию (interrupt mode).
5. Разработка функционального теста проверки взаимодействия СнК с датчиком температуры согласно протоколу взаимодействия.

6. Проверка вызова прерываний, вызванных блоком UART
7. Проверка обработки прерывани
