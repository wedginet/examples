window.detectUserActivity = (dotNetHelper) => {
    const resetActivity = () => {
        dotNetHelper.invokeMethodAsync('ResetTimer');
    };

    window.addEventListener('mousemove', resetActivity);
    window.addEventListener('keypress', resetActivity);
    window.addEventListener('touchstart', resetActivity);
    window.addEventListener('click', resetActivity);
};
